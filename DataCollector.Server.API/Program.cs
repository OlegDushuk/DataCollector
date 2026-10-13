using System.Text.Json.Serialization;
using DataCollector.Server.API.Infrastructure;
using DataCollector.Server.API.Monitoring;
using DataCollector.Server.Application.Interfaces.Notifications;
using DataCollector.Server.Application.Extensions;
using DataCollector.Server.DataAccess.Extensions;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddDataAccess();

// Моніторинг: зовнішній канал сповіщень (вебхук / Telegram) - до AddApplication, щоб замінити «канал не підключено»,
// і фоновий сервіс перевірки правил.
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection(MonitoringOptions.Section));
builder.Services.AddHttpClient<INotificationSender, HttpNotificationSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHostedService<MonitoringWorker>();

builder.Services.AddApplication();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
  // Специфікація доступна за адресою /openapi/v1.json
  app.MapOpenApi();
}

app.MapControllers();

app.Run();
