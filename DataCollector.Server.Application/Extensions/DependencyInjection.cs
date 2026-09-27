using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DataCollector.Server.Application.Extensions;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    services.AddTransient<IDataConfigService, DataConfigService>();
    
    return services;
  }
}