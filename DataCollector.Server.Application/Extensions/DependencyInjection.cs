using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.Interfaces.Caching;
using DataCollector.Server.Application.Services;
using DataCollector.Server.Application.Services.Internal;
using DataCollector.Server.Application.Interfaces.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataCollector.Server.Application.Extensions;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    // Кеш моделей - один на процес: модель потрібна кожному запиту до записів, а змінюється рідко.
    services.AddMemoryCache();
    services.AddSingleton<IEntityConfigCache, EntityConfigCache>();

    services.AddTransient<IDataConfigService, DataConfigService>();
    services.AddTransient<IDataInstanceService, DataInstanceService>();
    services.AddTransient<IDataExportService, DataExportService>();
    services.AddTransient<IDashboardService, DashboardService>();

    // Моніторинг: той самий сервіс - і для API (правила, сповіщення), і для фонової перевірки.
    // Зовнішній канал за замовчуванням не підключено; хост API реєструє свій (вебхук / Telegram), якщо налаштовано.
    services.AddTransient<MonitoringService>();
    services.AddTransient<IMonitoringService>(sp => sp.GetRequiredService<MonitoringService>());
    services.AddTransient<IMonitoringRunner>(sp => sp.GetRequiredService<MonitoringService>());
    services.TryAddSingleton<INotificationSender, NullNotificationSender>();
    
    return services;
  }
}
