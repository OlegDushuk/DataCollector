using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using DataCollector.Server.API.Monitoring;
using DataCollector.Server.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DataCollector.Server.API.Infrastructure;

/// <summary>
/// Перевірка стану для систем розгортання й спостереження (Docker, Kubernetes, балансувальник, Uptime-монітор):
/// <list type="bullet">
/// <item><c>/health/live</c> - процес живий (без перевірок залежностей; не пройшла - перезапустити);</item>
/// <item><c>/health/ready</c> - готовий обслуговувати запити: база даних доступна (не пройшла - не надсилати трафік);</item>
/// <item><c>/health</c> - звіт по всіх перевірках у JSON (база даних, фоновий моніторинг).</item>
/// </list>
/// Доступні без входу; у звіті лише стан і короткий опис, без рядків підключення і тексту винятків.
/// </summary>
public static class HealthChecks
{
  private const string ReadyTag = "ready";

  // Описи українською - без екранування кирилиці (\u0411…), щоб звіт можна було прочитати очима.
  private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web)
  {
    WriteIndented = true,
    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
  };

  public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
  {
    services.AddSingleton<MonitoringWorkerStatus>();

    services.AddHealthChecks()
      .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag])
      .AddCheck<MonitoringHealthCheck>("monitoring");

    return services;
  }

  public static void MapApiHealthChecks(this IEndpointRouteBuilder endpoints)
  {
    endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
      .AllowAnonymous();

    endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
      .AllowAnonymous();

    endpoints.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteReport })
      .AllowAnonymous();
  }

  /// <summary>{ "status": "Healthy", "durationMs": 12, "checks": { "database": { "status", "description", "durationMs" } } }</summary>
  private static Task WriteReport(HttpContext context, HealthReport report)
  {
    context.Response.ContentType = "application/json; charset=utf-8";

    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
      status = report.Status.ToString(),
      durationMs = Math.Round(report.TotalDuration.TotalMilliseconds),
      checks = report.Entries.ToDictionary(e => e.Key, e => new
      {
        status = e.Value.Status.ToString(),
        description = e.Value.Description,
        durationMs = Math.Round(e.Value.Duration.TotalMilliseconds)
      })
    }, ReportJson));
  }
}

/// <summary>База даних відповідає на простий запит не довше ніж за 5 с.</summary>
public sealed class DatabaseHealthCheck(IDatabaseHealth database, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
  private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

  public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
  {
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(Timeout);

    try
    {
      await database.Ping(timeout.Token);
      return HealthCheckResult.Healthy("База даних доступна");
    }
    catch (Exception e) when (!cancellationToken.IsCancellationRequested)
    {
      logger.LogWarning(e, "Перевірка стану: база даних недоступна");
      return HealthCheckResult.Unhealthy(timeout.IsCancellationRequested
        ? "База даних не відповіла за 5 с"
        : "База даних недоступна");
    }
  }
}

/// <summary>
/// Фоновий моніторинг працює: останній прохід був нещодавно (не довше трьох інтервалів тому) і без помилки.
/// Збій моніторингу не робить API неготовим (дані приймаються далі), тому стан - Degraded, а не Unhealthy.
/// </summary>
public sealed class MonitoringHealthCheck(IOptions<MonitoringOptions> options, MonitoringWorkerStatus status) : IHealthCheck
{
  public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
  {
    if (!options.Value.Enabled)
      return Task.FromResult(HealthCheckResult.Healthy("Фоновий моніторинг вимкнено налаштуваннями"));

    var tick = TimeSpan.FromSeconds(Math.Clamp(options.Value.TickSeconds, 5, 300));
    var allowed = tick * 3 + TimeSpan.FromSeconds(10);
    var now = DateTime.UtcNow;

    var result = status.LastPassAt switch
    {
      null when now - status.StartedAt < allowed => HealthCheckResult.Healthy("Фоновий моніторинг запускається"),
      null => HealthCheckResult.Degraded("Фоновий моніторинг не виконав жодної перевірки"),
      { } last when now - last > allowed => HealthCheckResult.Degraded($"Остання перевірка правил - {(int)(now - last).TotalSeconds} с тому"),
      _ when status.LastError != null => HealthCheckResult.Degraded("Остання перевірка правил завершилась з помилкою"),
      _ => HealthCheckResult.Healthy("Правила перевіряються")
    };

    return Task.FromResult(result);
  }
}
