using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace DataCollector.Server.DataAccess.Extensions;

public static class DependencyInjection
{
  public static IServiceCollection AddDataAccess(this IServiceCollection services)
  {
    services.AddTransient<IEntityConfigRepository, EntityConfigRepository>();
    services.AddTransient<IEntityPropertyConfigRepository, EntityPropertyConfigRepository>();
    
    return services;
  }
}