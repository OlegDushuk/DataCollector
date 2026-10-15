using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace DataCollector.Server.DataAccess.Extensions;

public static class DependencyInjection
{
  public static IServiceCollection AddDataAccess(this IServiceCollection services)
  {
    // Репозиторії без стану (рядок підключення читається один раз, з'єднання - на кожен запит з пулу),
    // тож один екземпляр на процес - і їх може використовувати кеш моделей (singleton).
    services.AddSingleton<IEntityConfigRepository, EntityConfigRepository>();
    services.AddSingleton<IEntityPropertyConfigRepository, EntityPropertyConfigRepository>();
    services.AddSingleton<IEntityInstanceRepository, EntityInstanceRepository>();
    services.AddSingleton<IDashboardRepository, DashboardRepository>();
    services.AddSingleton<IMonitoringRuleRepository, MonitoringRuleRepository>();
    services.AddSingleton<INotificationRepository, NotificationRepository>();
    services.AddSingleton<IUserRepository, UserRepository>();
    services.AddSingleton<IApiKeyRepository, ApiKeyRepository>();
    services.AddSingleton<IDatabaseHealth, DatabaseHealth>();
    
    return services;
  }
}
