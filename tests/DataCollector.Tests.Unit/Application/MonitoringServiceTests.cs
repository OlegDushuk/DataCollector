using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Application.Services;
using DataCollector.Server.Application.Services.Internal;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Querying;
using DataCollector.Tests.Shared;
using Microsoft.Extensions.Caching.Memory;

namespace DataCollector.Tests.Unit.Application;

/// <summary>
/// Сервіс моніторингу: показник порівняння рахується окремим запитом агрегації - за своєю моделлю, умовами й періодом.
/// Агрегація виконується в SQL Server, тож тут її підміняє заглушка, що запам'ятовує запити й повертає задані значення.
/// </summary>
public class MonitoringServiceTests
{
  private readonly InMemoryStore _store = new();
  private readonly FakeAggregation _aggregation = new();
  private readonly EntityConfig _orders;
  private readonly EntityConfig _returns;

  public MonitoringServiceTests()
  {
    _orders = TestModels.Orders();
    _returns = new EntityConfig("returns", "Повернення");
    _returns.AddProperty("amount", "Сума повернення", PropertyDataType.Number);

    _store.Configs[_orders.Id] = _orders;
    _store.Configs[_returns.Id] = _returns;
  }

  private MonitoringService Service()
  {
    var configs = new EntityConfigCache(new InMemoryEntityConfigRepository(_store), new MemoryCache(new MemoryCacheOptions()));
    return new MonitoringService(new InMemoryMonitoringRuleRepository(_store), new InMemoryNotificationRepository(), configs,
      _aggregation, new NullNotificationSender());
  }

  private static SaveMonitoringRuleCommand SalesGrowth(MonitoringCompareCommand compare) => new()
  {
    Config = "orders", Name = "Продажі ростуть", Function = "sum", Field = "price", WindowUnit = "month",
    Operator = "gt", TimeZone = "UTC", CheckIntervalSeconds = 60, Compare = compare
  };

  [Fact]
  public async Task Preview_ComparesWithPreviousMonth()
  {
    _aggregation.Values.Enqueue(1200m);   // поточний місяць
    _aggregation.Values.Enqueue(1000m);   // попередній місяць до того ж моменту

    var preview = await Service().Preview(SalesGrowth(new MonitoringCompareCommand
    {
      Config = "orders", Function = "sum", Field = "price", WindowUnit = "month", WindowOffset = 1, WindowToDate = true,
      Multiplier = 1.1m
    }));

    Assert.Equal("alert", preview.State);
    Assert.Equal(1200m, preview.Value);
    Assert.Equal(1000m, preview.CompareValue);
    Assert.Equal(1100m, preview.Threshold);
    Assert.Equal("Сума: Сума за поточний місяць > Сума: Сума за попередній місяць до того ж моменту × 1,1", preview.Condition);

    // Два запити: поточний місяць (від і до) і попередній (від і до того ж моменту).
    Assert.Equal(2, _aggregation.Queries.Count);
    Assert.All(_aggregation.Queries, q => Assert.Equal(2, q.Query.Filters.Count));
  }

  [Fact]
  public async Task Preview_CompareFromAnotherModel_NamesTheModel()
  {
    _aggregation.Values.Enqueue(1000m);
    _aggregation.Values.Enqueue(30m);

    var command = SalesGrowth(new MonitoringCompareCommand
    {
      Config = "returns", Function = "sum", Field = "amount", WindowUnit = "month", Multiplier = 0.05m
    });
    command.Operator = "lt";

    var preview = await Service().Preview(command);

    // Повернення 30 < 5% від продажів 1000 = 50 - умова «продажі менші» не виконується, норма.
    Assert.Equal("ok", preview.State);
    Assert.Equal(_returns.Id, _aggregation.Queries[1].Config.Id);
    Assert.Contains("Сума: Сума повернення («Повернення») за поточний місяць × 0,05", preview.Condition);
  }

  [Fact]
  public async Task Preview_NoDataForComparison_IsNoData()
  {
    _aggregation.Values.Enqueue(1200m);
    _aggregation.Values.Enqueue(null);

    var preview = await Service().Preview(SalesGrowth(new MonitoringCompareCommand
    {
      Config = "orders", Function = "sum", Field = "price", WindowUnit = "month", WindowOffset = 1
    }));

    Assert.Equal("noData", preview.State);
    Assert.StartsWith("немає даних для порівняння", preview.Error);
  }

  [Fact]
  public async Task CreateRule_RoundTripsComparison()
  {
    var service = Service();
    var id = await service.CreateRule(SalesGrowth(new MonitoringCompareCommand
    {
      Config = "returns", Function = "count", WindowMinutes = 1440, WindowOffset = 1, Multiplier = 2
    }));

    var rule = await service.GetRule(id);

    Assert.Equal("month", rule.WindowUnit);
    Assert.NotNull(rule.Compare);
    Assert.Equal(_returns.Id, rule.Compare!.ConfigId);
    Assert.Equal("Повернення", rule.Compare.ConfigName);
    Assert.Equal("count", rule.Compare.Function);
    Assert.Equal(1440, rule.Compare.WindowMinutes);
    Assert.Equal(1, rule.Compare.WindowOffset);
    Assert.Equal(2m, rule.Compare.Multiplier);
  }

  [Fact]
  public async Task CreateRule_UnknownCompareModel_IsValidationError()
  {
    await Assert.ThrowsAsync<DomainValidationException>(() => Service().CreateRule(SalesGrowth(new MonitoringCompareCommand
    {
      Config = "nope", Function = "count"
    })));
  }

  /// <summary>Агрегація-заглушка: повертає значення з черги по одному на запит і запам'ятовує запити.</summary>
  private sealed class FakeAggregation : IEntityInstanceRepository
  {
    public Queue<decimal?> Values { get; } = new();
    public List<(EntityConfig Config, AggregateQuery Query)> Queries { get; } = [];

    public Task<AggregateResult> Aggregate(EntityConfig config, AggregateQuery query, CancellationToken cancellationToken = default)
    {
      Queries.Add((config, query));
      var value = Values.Count > 0 ? Values.Dequeue() : null;
      return Task.FromResult(new AggregateResult([new AggregateRow([], [value])], false));
    }

    public Task<(List<EntityInstance> Items, int TotalCount)> Query(EntityConfig config, RecordQuery query, CancellationToken cancellationToken = default)
      => throw new NotSupportedException();

    public Task<EntityInstance?> GetById(EntityConfig config, Guid id) => throw new NotSupportedException();
    public Task Create(EntityInstance instance) => throw new NotSupportedException();
    public Task<long> Update(EntityInstance instance) => throw new NotSupportedException();
    public Task Delete(Guid id, long? expectedVersion = null) => throw new NotSupportedException();

    public Task<Dictionary<Guid, Guid>> GetOwners(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
      => throw new NotSupportedException();

    public Task Import(EntityConfig config, IReadOnlyList<EntityInstance> created, IReadOnlyList<EntityInstance> updated,
      IReadOnlyCollection<Guid> updatedPropertyIds, CancellationToken cancellationToken = default)
      => throw new NotSupportedException();
  }
}
