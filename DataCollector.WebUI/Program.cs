using DataCollector.WebUI.Auth;
using DataCollector.WebUI.Entities;
using DataCollector.WebUI.ServerApiServices;
using DataCollector.WebUI.Services;
using DataCollector.WebUI.Views;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .CreateBootstrapLogger();

try
{
  var builder = WebApplication.CreateBuilder(args);

  // Serilog: консоль і щоденні файли logs/webui-*.log, рівні - у секції "Serilog".
  builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

  builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

  // Вхід: після перевірки логіна й пароля в API інтерфейс зберігає токен API в зашифрованій cookie
  // (HttpOnly - недоступна JavaScript) і передає його з кожним запитом до API від імені користувача.
  builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
      options.Cookie.Name = ".DataCollector.Auth";
      options.Cookie.HttpOnly = true;
      options.Cookie.SameSite = SameSiteMode.Lax;
      options.LoginPath = "/login";
      options.AccessDeniedPath = "/access-denied";
      options.ReturnUrlParameter = "returnUrl";
      // Час життя cookie задає вхід (дорівнює часу життя токена API).
      options.SlidingExpiration = false;
    });

  builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.CanEdit, policy => policy.RequireRole(UserRoles.Editor, UserRoles.Admin))
    .AddPolicy(AuthPolicies.CanAdmin, policy => policy.RequireRole(UserRoles.Admin));

  builder.Services.AddCascadingAuthenticationState();

  // Перебір паролів через форму входу: спроб входу з однієї IP-адреси за хвилину (секція "LoginRateLimit").
  // API обмежує вхід ще й сам, але воно бачить лише адресу цього сервера - справжню адресу клієнта знає інтерфейс.
  var loginAttemptsPerMinute = builder.Configuration.GetValue("LoginRateLimit:AttemptsPerMinute", 10);
  builder.Services.AddRateLimiter(limiter =>
  {
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
      HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/login") && loginAttemptsPerMinute > 0
        ? RateLimitPartition.GetFixedWindowLimiter(
            "login:" + context.Connection.RemoteIpAddress,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = loginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("other"));
    limiter.OnRejected = async (context, cancellationToken) =>
    {
      context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
        .LogWarning("Забагато спроб входу з адреси {ClientIp}", context.HttpContext.Connection.RemoteIpAddress);

      context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
      await context.HttpContext.Response.WriteAsync(
        "<!DOCTYPE html><html lang=\"uk\"><head><meta charset=\"utf-8\"><title>Забагато спроб входу</title></head>" +
        "<body style=\"font-family:sans-serif;padding:40px\"><h1>Забагато спроб входу</h1>" +
        "<p>Зачекайте хвилину й спробуйте знову.</p><p><a href=\"/login\">Повернутися до входу</a></p></body></html>",
        cancellationToken);
    };
  });

  var apiBaseUrl = builder.Configuration["ServerApi:BaseUrl"]
                   ?? throw new InvalidOperationException("ServerApi:BaseUrl is not configured");

  builder.Services.AddScoped<CurrentUser>();
  builder.Services.AddScoped<ApiContext>();
  builder.Services.AddScoped<BrowserStorage>();
  builder.Services.AddScoped<LayoutState>();
  builder.Services.AddHttpClient<AuthApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
  builder.Services.AddHttpClient<UsersApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
  builder.Services.AddHttpClient<EntityConfigApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
  builder.Services.AddHttpClient<DashboardApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
  builder.Services.AddHttpClient<MonitoringApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
  builder.Services.AddScoped<MonitoringState>();
  builder.Services.AddHttpClient<EntityRecordApiService>(c =>
  {
    c.BaseAddress = new Uri(apiBaseUrl);

    // Експорт до 100 000 записів (або імпорт такого файлу) може тривати довше за стандартні 100 с.
    // Звичайні запити таблиці від цього не «зависають»: сторінка скасовує їх сама, коли користувач іде далі.
    c.Timeout = TimeSpan.FromMinutes(5);
  });

  // Перевірка стану: /health/live - процес живий, /health - готовність (API і база даних доступні).
  builder.Services.AddHttpClient(ApiHealthCheck.HttpClientName);
  builder.Services.AddHealthChecks().AddCheck<ApiHealthCheck>("api");

  var app = builder.Build();

  if (!app.Environment.IsDevelopment())
  {
    // Непередбачена помилка - сторінка /error замість порожньої відповіді (у Development - детальна сторінка).
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
  }

  // Журнал запитів сторінок: статичні файли й з'єднання Blazor - лише на рівні Debug.
  app.UseSerilogRequestLogging(options =>
  {
    options.GetLevel = (context, _, exception) =>
      exception != null || context.Response.StatusCode >= 500
        ? Serilog.Events.LogEventLevel.Error
        : context.Request.Path.StartsWithSegments("/_blazor") || context.Request.Path.StartsWithSegments("/_framework") ||
          context.Request.Path.StartsWithSegments("/dist") || Path.HasExtension(context.Request.Path.Value)
          ? Serilog.Events.LogEventLevel.Debug
          : Serilog.Events.LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnostics, context) =>
      diagnostics.Set("UserName", context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : "anonymous");
  });

  // Невідома адреса при першому відкритті - сторінка «Не знайдено» з меню, а не порожня 404 браузера.
  app.UseStatusCodePagesWithReExecute("/not-found");

  app.UseRateLimiter();

  app.UseAuthentication();
  app.UseAuthorization();
  app.UseAntiforgery();

  app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
  app.MapHealthChecks("/health");

  app.MapStaticAssets();
  app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

  // Вихід: звичайна форма POST з меню. Захист від підробки - SameSite=Lax (чужий сайт не надішле cookie в POST).
  app.MapPost("/account/logout", async (HttpContext context, ILogger<Program> logger) =>
  {
    logger.LogInformation("Користувач {Login} вийшов", context.User.Identity?.Name);
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("~/login");
  }).DisableAntiforgery();

  app.Run();
}
catch (Exception e) when (e is not HostAbortedException)
{
  Log.Fatal(e, "Веб-інтерфейс зупинено через непередбачену помилку");
}
finally
{
  Log.CloseAndFlush();
}
