using System.Text.Json;
using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.ExternalContracts.Models;

namespace DataCollector.Tests.Unit.Application;

/// <summary>
/// Оптимістична конкурентність: зміна, зроблена за застарілою версією, відхиляється (409),
/// а не перезаписує чужі зміни. Без версії - останнє збереження перемагає.
/// </summary>
public class ConcurrencyTests
{
  private readonly ServiceFactory _services = new();

  private static Dictionary<string, JsonElement> Values(params (string Key, object? Value)[] values)
    => values.ToDictionary(v => v.Key, v => JsonSerializer.SerializeToElement(v.Value));

  private async Task<(Guid Id, long Version)> CreateOrder()
  {
    var config = _services.AddModel(TestModels.Orders());
    var records = _services.Records();
    var id = await records.CreateInstance(config.Key, new CreateDataInstanceCommand { Values = Values(("number", "ORD-1"), ("price", 100)) });
    return (id, (await records.Get(config.Key, id)).Version);
  }

  [Fact]
  public async Task Update_WithCurrentVersion_Succeeds_AndReturnsNewVersion()
  {
    var (id, version) = await CreateOrder();
    var records = _services.Records();

    var newVersion = await records.UpdateInstance("orders", id, new UpdateDataInstanceCommand { Values = Values(("price", 150)), Version = version });

    Assert.NotEqual(version, newVersion);
    var record = await records.Get("orders", id);
    Assert.Equal(newVersion, record.Version);
    Assert.Equal(150m, record.Values["price"]);
  }

  [Fact]
  public async Task Update_WithStaleVersion_IsRejected_AndDataUnchanged()
  {
    var (id, version) = await CreateOrder();
    var records = _services.Records();

    // Інший користувач зберіг запис після того, як цей його відкрив.
    await records.UpdateInstance("orders", id, new UpdateDataInstanceCommand { Values = Values(("price", 200)), Version = version });

    await Assert.ThrowsAsync<VersionConflictException>(() =>
      records.UpdateInstance("orders", id, new UpdateDataInstanceCommand { Values = Values(("price", 999)), Version = version }));

    Assert.Equal(200m, (await records.Get("orders", id)).Values["price"]);
  }

  [Fact]
  public async Task Update_WithoutVersion_LastWriteWins()
  {
    var (id, version) = await CreateOrder();
    var records = _services.Records();
    await records.UpdateInstance("orders", id, new UpdateDataInstanceCommand { Values = Values(("price", 200)), Version = version });

    await records.UpdateInstance("orders", id, new UpdateDataInstanceCommand { Values = Values(("price", 300)) });

    Assert.Equal(300m, (await records.Get("orders", id)).Values["price"]);
  }

  [Fact]
  public async Task Delete_WithStaleVersion_IsRejected()
  {
    var (id, version) = await CreateOrder();
    var records = _services.Records();
    await records.UpdateInstance("orders", id, new UpdateDataInstanceCommand { Values = Values(("price", 200)) });

    await Assert.ThrowsAsync<VersionConflictException>(() => records.DeleteInstance("orders", id, version));
    Assert.NotNull(await records.Get("orders", id));
  }

  [Fact]
  public async Task Dashboard_SaveWithStaleVersion_IsRejected()
  {
    var dashboards = _services.Dashboards();
    var id = await dashboards.Create(new SaveDashboardCommand { Name = "Продажі" });
    var opened = await dashboards.Get(id);

    var saved = await dashboards.Save(id, new SaveDashboardCommand { Name = "Продажі за день", Version = opened.Version });
    var error = await Assert.ThrowsAsync<VersionConflictException>(() =>
      dashboards.Save(id, new SaveDashboardCommand { Name = "Продажі за тиждень", Version = opened.Version }));

    Assert.Contains("Дашборд змінено", error.Message);
    Assert.Equal("Продажі за день", (await dashboards.Get(id)).Name);
    Assert.Equal(saved, (await dashboards.Get(id)).Version);
  }

  [Fact]
  public async Task MonitoringRule_UpdateWithStaleVersion_IsRejected()
  {
    var config = _services.AddModel(TestModels.Orders());
    var monitoring = _services.Monitoring();
    var command = new SaveMonitoringRuleCommand
    {
      Config = config.Key, Name = "Великі продажі", Function = "sum", Field = "price", WindowMinutes = 60,
      Operator = "gt", Threshold = 1000, CheckIntervalSeconds = 60
    };
    var id = await monitoring.CreateRule(command);
    var opened = await monitoring.GetRule(id);

    command.Threshold = 2000;
    command.Version = opened.Version;
    await monitoring.UpdateRule(id, command);

    command.Threshold = 3000;   // друга вкладка з тією ж, уже застарілою версією
    await Assert.ThrowsAsync<VersionConflictException>(() => monitoring.UpdateRule(id, command));
    Assert.Equal(2000, (await monitoring.GetRule(id)).Threshold);
  }

  [Fact]
  public void VersionConflict_IsConflict_So409()
  {
    Assert.IsAssignableFrom<ConflictException>(new VersionConflictException(VersionConflictException.Record));
  }
}
