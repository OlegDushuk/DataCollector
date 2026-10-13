using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.Interfaces.Caching;
using DataCollector.Server.Application.Services;
using DataCollector.Server.Application.Services.Internal;
using Microsoft.Extensions.DependencyInjection;

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
    
    return services;
  }
}
