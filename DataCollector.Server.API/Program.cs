using System.Text.Json.Serialization;
using DataCollector.Server.API.Auth;
using DataCollector.Server.API.Infrastructure;
using DataCollector.Server.API.Monitoring;
using DataCollector.Server.Application.Interfaces.Notifications;
using DataCollector.Server.Application.Extensions;
using DataCollector.Server.DataAccess.Extensions;
using DataCollector.Server.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Serilog;

// Журнал запуску - до того, як прочитано налаштування: помилка конфігурації теж має потрапити в консоль.
Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .CreateBootstrapLogger();

try
{
  var builder = WebApplication.CreateBuilder(args);

  // Serilog: консоль і щоденні файли logs/api-*.log (JSON - для пошуку й аналізу), рівні - у секції "Serilog".
  builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

  builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
      // Типи полів у JSON як рядки ("Number"), числа на вході теж приймаються.
      options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

  builder.Services.AddProblemDetails();
  builder.Services.AddExceptionHandler<ApiExceptionHandler>();
  builder.Services.AddOpenApi();

  // Автентифікація: токен користувача (після входу) або API-ключ зовнішньої системи.
  builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.Section));
  var authOptions = builder.Configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();

  builder.Services
    .AddAuthentication(AuthSchemes.Default)
    .AddPolicyScheme(AuthSchemes.Default, "Токен або API-ключ", options =>
    {
      options.ForwardDefaultSelector = context => context.Request.Headers.ContainsKey(AuthSchemes.ApiKeyHeader)
        ? AuthSchemes.ApiKey
        : AuthSchemes.Bearer;
    })
    .AddBearerToken(AuthSchemes.Bearer, options => options.BearerTokenExpiration = authOptions.TokenLifetime)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
      AuthSchemes.ApiKey, configureOptions: null);

  // Без атрибута - доступно будь-кому після входу (перегляд); зміни - редактору, керування - адміністратору.
  builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(AuthPolicies.CanEdit, policy => policy.RequireRole(AuthClaims.RolesFrom(UserRole.Editor)))
    .AddPolicy(AuthPolicies.CanAdmin, policy => policy.RequireRole(AuthClaims.RolesFrom(UserRole.Admin)));

  builder.Services.AddHostedService<AuthBootstrapper>();

  builder.Services.AddDataAccess();

  // Моніторинг: зовнішній канал сповіщень (вебхук / Telegram) - до AddApplication, щоб замінити «канал не підключено»,
  // і фоновий сервіс перевірки правил.
  builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection(MonitoringOptions.Section));
  builder.Services.AddHttpClient<INotificationSender, HttpNotificationSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
  builder.Services.AddHostedService<MonitoringWorker>();

  builder.Services.AddApplication();

  var app = builder.Build();

  // Один рядок журналу на запит: метод, адреса, код, тривалість, хто виконав.
  app.UseSerilogRequestLogging(RequestLogging.Configure);

  app.UseExceptionHandler();

  // 401/403/404 без тіла -> Problem Details, як і решта помилок API.
  app.UseStatusCodePages();

  app.UseAuthentication();
  app.UseMiddleware<SessionValidationMiddleware>();
  app.UseAuthorization();

  if (app.Environment.IsDevelopment())
  {
    // Специфікація доступна за адресою /openapi/v1.json
    app.MapOpenApi().AllowAnonymous();
  }

  app.MapControllers();

  app.Run();
}
catch (Exception e) when (e is not HostAbortedException)
{
  Log.Fatal(e, "API зупинено через непередбачену помилку");
}
finally
{
  Log.CloseAndFlush();
}
