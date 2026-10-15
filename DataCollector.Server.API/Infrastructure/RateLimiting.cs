using System.Security.Claims;
using System.Threading.RateLimiting;
using DataCollector.Server.API.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DataCollector.Server.API.Infrastructure;

/// <summary>Ліміти частоти запитів (секція "RateLimiting" в appsettings).</summary>
public sealed class RateLimitingOptions
{
  public const string Section = "RateLimiting";

  public bool Enabled { get; set; } = true;

  /// <summary>Запитів за хвилину в середньому на одного користувача або API-ключ.</summary>
  public int RequestsPerMinute { get; set; } = 300;

  /// <summary>Скільки запитів поспіль можна зробити понад середній темп (наприклад, дашборд з багатьма віджетами).</summary>
  public int Burst { get; set; } = 100;

  /// <summary>Запитів за хвилину без входу - з однієї IP-адреси.</summary>
  public int AnonymousRequestsPerMinute { get; set; } = 60;

  /// <summary>
  /// Спроб входу за хвилину з однієї IP-адреси (додатково до блокування облікового запису після 5 невдалих спроб).
  /// Веб-інтерфейс звертається до API з однієї адреси, тому окремий ліміт на клієнта діє ще й в інтерфейсі.
  /// </summary>
  public int LoginAttemptsPerMinute { get; set; } = 30;

  /// <summary>Одночасних «важких» запитів (імпорт, експорт) від одного клієнта; решта отримує 429 одразу.</summary>
  public int ConcurrentHeavyRequests { get; set; } = 2;
}

/// <summary>Іменовані політики для атрибута [EnableRateLimiting].</summary>
public static class RateLimitPolicies
{
  public const string Login = "login";
  public const string Heavy = "heavy";
}

/// <summary>
/// Захист від перевантаження і перебору: загальний ліміт на клієнта (маркерний кошик - token bucket),
/// окремі ліміти на вхід (фіксоване вікно на IP) і на імпорт / експорт (кількість одночасних запитів).
/// Перевищення - 429 Too Many Requests у форматі Problem Details із заголовком Retry-After.
/// </summary>
public static class RateLimiting
{
  private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

  public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
  {
    var options = configuration.GetSection(RateLimitingOptions.Section).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

    services.AddRateLimiter(limiter =>
    {
      limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
      limiter.OnRejected = OnRejected;

      // Загальний ліміт: кожен користувач / ключ має власний «кошик» запитів, анонімні запити - спільний на IP.
      limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
      {
        var client = ClientKey(context);

        if (!options.Enabled || IsExcluded(context))
          return RateLimitPartition.GetNoLimiter(client);

        if (IsAnonymous(context))
        {
          return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
          {
            PermitLimit = Math.Max(1, options.AnonymousRequestsPerMinute),
            Window = Minute,
            QueueLimit = 0
          });
        }

        // Маркерний кошик: до Burst запитів одразу, далі - RequestsPerMinute за хвилину (поповнення кожні 10 с).
        return RateLimitPartition.GetTokenBucketLimiter(client, _ => new TokenBucketRateLimiterOptions
        {
          TokenLimit = Math.Max(1, options.Burst),
          TokensPerPeriod = Math.Max(1, options.RequestsPerMinute / 6),
          ReplenishmentPeriod = TimeSpan.FromSeconds(10),
          QueueLimit = 0,
          AutoReplenishment = true
        });
      });

      limiter.AddPolicy(RateLimitPolicies.Login, context => options.Enabled
        ? RateLimitPartition.GetFixedWindowLimiter("login:" + ClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = Math.Max(1, options.LoginAttemptsPerMinute),
          Window = Minute,
          QueueLimit = 0
        })
        : RateLimitPartition.GetNoLimiter("login"));

      limiter.AddPolicy(RateLimitPolicies.Heavy, context => options.Enabled
        ? RateLimitPartition.GetConcurrencyLimiter("heavy:" + ClientKey(context), _ => new ConcurrencyLimiterOptions
        {
          PermitLimit = Math.Max(1, options.ConcurrentHeavyRequests),
          QueueLimit = 0
        })
        : RateLimitPartition.GetNoLimiter("heavy"));
    });

    return services;
  }

  /// <summary>Хто робить запит: користувач, API-ключ або (без входу) IP-адреса.</summary>
  public static string ClientKey(HttpContext context)
  {
    var user = context.User;
    if (user.Identity?.IsAuthenticated == true && user.FindFirstValue(ClaimTypes.NameIdentifier) is { } id)
      return (user.FindFirstValue(AuthClaims.Kind) == AuthClaims.KindApiKey ? "key:" : "user:") + id;

    return "ip:" + ClientIp(context);
  }

  private static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

  private static bool IsAnonymous(HttpContext context) => context.User.Identity?.IsAuthenticated != true;

  /// <summary>Перевірка стану (/health) - для систем спостереження, без ліміту.</summary>
  private static bool IsExcluded(HttpContext context) => context.Request.Path.StartsWithSegments("/health");

  private static async ValueTask OnRejected(OnRejectedContext context, CancellationToken cancellationToken)
  {
    var http = context.HttpContext;
    var detail = "Забагато запитів. Повторіть спробу трохи пізніше";

    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
    {
      var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
      http.Response.Headers.RetryAfter = seconds.ToString();
      detail = $"Забагато запитів. Повторіть спробу через {seconds} с";
    }

    http.RequestServices.GetRequiredService<ILoggerFactory>()
      .CreateLogger("DataCollector.Server.API.RateLimiting")
      .LogWarning("Перевищено ліміт запитів: {Client} {Method} {Path}", ClientKey(http), http.Request.Method, http.Request.Path);

    await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
    {
      HttpContext = http,
      ProblemDetails = new ProblemDetails
      {
        Status = StatusCodes.Status429TooManyRequests,
        Title = "Забагато запитів",
        Detail = detail
      }
    });
  }
}
