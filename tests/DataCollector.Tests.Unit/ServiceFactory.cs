using DataCollector.Server.Application.Services;
using DataCollector.Server.Application.Services.Internal;
using DataCollector.Server.Domain.Entities;
using DataCollector.Tests.Shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace DataCollector.Tests.Unit;

/// <summary>Сервіси застосунку поверх сховища в пам'яті.</summary>
internal sealed class ServiceFactory
{
  public InMemoryStore Store { get; } = new();
  public InMemoryEntityInstanceRepository Instances { get; }
  public InMemoryMonitoringRuleRepository Rules { get; }
  public EntityConfigCache Configs { get; }

  public ServiceFactory()
  {
    Instances = new InMemoryEntityInstanceRepository(Store);
    Rules = new InMemoryMonitoringRuleRepository(Store);
    Configs = new EntityConfigCache(new InMemoryEntityConfigRepository(Store), new MemoryCache(new MemoryCacheOptions()));
  }

  public EntityConfig AddModel(EntityConfig config)
  {
    Store.Configs[config.Id] = config;
    return config;
  }

  public DataInstanceService Records() => new(Configs, Instances);

  public DashboardService Dashboards() => new(new InMemoryDashboardRepository(Store));

  public MonitoringService Monitoring() => new(Rules, new InMemoryNotificationRepository(), Configs, Instances, new NullNotificationSender());

  public DataImportService Import() => new(Configs, Instances, NullLogger<DataImportService>.Instance);
}
