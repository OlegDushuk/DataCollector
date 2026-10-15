using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Monitoring;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Tests.Unit.Domain;

/// <summary>Правило моніторингу: перевірка визначення і переходи станів за результатами перевірок.</summary>
public class MonitoringRuleTests
{
  private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
  private readonly Server.Domain.Entities.EntityConfig _orders = TestModels.Orders();

  private static RuleDefinition Definition(
    AggregateFunction function = AggregateFunction.Sum,
    string? field = "price",
    int consecutive = 1,
    RuleOperator op = RuleOperator.GreaterThan,
    decimal threshold = 1000,
    int interval = 60,
    string timeField = "createdAt")
    => new("Великі продажі", true, null, null, function, field, timeField, 60, op, threshold, consecutive, interval);

  [Fact]
  public void FirstCheck_Ok_SetsStateWithoutNotification()
  {
    var rule = MonitoringRule.Create(Definition(), _orders);

    var notification = rule.ApplyCheck(500, null, Now, "Сума");

    Assert.Null(notification);
    Assert.Equal(RuleState.Ok, rule.State);
  }

  [Fact]
  public void FirstCheck_Alert_Notifies()
  {
    var rule = MonitoringRule.Create(Definition(), _orders);

    var notification = rule.ApplyCheck(1500, null, Now, "Сума");

    Assert.NotNull(notification);
    Assert.Equal(RuleState.Unknown, notification.FromState);
    Assert.Equal(RuleState.Alert, notification.ToState);
    Assert.Contains("спрацювало", notification.Message);
  }

  [Fact]
  public void ConsecutiveChecks_StateChangesOnlyAfterNInARow()
  {
    var rule = MonitoringRule.Create(Definition(consecutive: 3), _orders);
    rule.ApplyCheck(500, null, Now, "Сума");

    Assert.Null(rule.ApplyCheck(1500, null, Now.AddMinutes(1), "Сума"));
    Assert.Null(rule.ApplyCheck(1500, null, Now.AddMinutes(2), "Сума"));
    Assert.Equal(RuleState.Ok, rule.State);
    Assert.Equal(2, rule.PendingCount);

    var notification = rule.ApplyCheck(1500, null, Now.AddMinutes(3), "Сума");

    Assert.NotNull(notification);
    Assert.Equal(RuleState.Alert, rule.State);
    Assert.Equal(0, rule.PendingCount);
  }

  [Fact]
  public void ConsecutiveChecks_InterruptedSeries_StartsOver()
  {
    var rule = MonitoringRule.Create(Definition(consecutive: 2), _orders);
    rule.ApplyCheck(500, null, Now, "Сума");

    rule.ApplyCheck(1500, null, Now.AddMinutes(1), "Сума");
    rule.ApplyCheck(500, null, Now.AddMinutes(2), "Сума");   // знову норма - серія обірвалась
    rule.ApplyCheck(1500, null, Now.AddMinutes(3), "Сума");

    Assert.Equal(RuleState.Ok, rule.State);
    Assert.Equal(1, rule.PendingCount);
  }

  [Fact]
  public void NoValue_Or_Error_MeansNoData()
  {
    var rule = MonitoringRule.Create(Definition(), _orders);
    rule.ApplyCheck(500, null, Now, "Сума");

    var notification = rule.ApplyCheck(null, "Поле видалено", Now.AddMinutes(1), "Сума");

    Assert.Equal(RuleState.NoData, rule.State);
    Assert.Equal("Поле видалено", rule.LastError);
    Assert.Contains("немає даних", notification!.Message);
  }

  [Theory]
  [InlineData(RuleOperator.GreaterThan, 10, 10, false)]
  [InlineData(RuleOperator.GreaterOrEqual, 10, 10, true)]
  [InlineData(RuleOperator.LessThan, 9, 10, true)]
  [InlineData(RuleOperator.Equal, 10, 10, true)]
  [InlineData(RuleOperator.NotEqual, 10, 10, false)]
  public void Operators_CompareWithThreshold(RuleOperator op, decimal value, decimal threshold, bool expected)
  {
    Assert.Equal(expected, op.Matches(value, threshold));
  }

  [Fact]
  public void Definition_MetricFieldMustBeNumber()
  {
    Assert.Throws<DomainValidationException>(() => MonitoringRule.Create(Definition(field: "number"), _orders));
  }

  [Fact]
  public void Definition_TimeFieldMustBeDateTime()
  {
    Assert.Throws<DomainValidationException>(() => MonitoringRule.Create(Definition(timeField: "deliveryDate"), _orders));
    Assert.Equal("paidAt", MonitoringRule.Create(Definition(timeField: "paidAt"), _orders).TimeField);
  }

  [Theory]
  [InlineData(0, 60)]
  [InlineData(11, 60)]
  [InlineData(1, 10)]
  public void Definition_LimitsChecked(int consecutive, int interval)
  {
    Assert.Throws<DomainValidationException>(() =>
      MonitoringRule.Create(Definition(consecutive: consecutive, interval: interval), _orders));
  }

  [Fact]
  public void Change_IncrementsVersion_AndKeepsState()
  {
    var rule = MonitoringRule.Create(Definition(), _orders);
    rule.ApplyCheck(1500, null, Now, "Сума");
    var version = rule.Version;

    rule.Change(Definition(threshold: 2000), _orders);

    Assert.Equal(version + 1, rule.Version);
    Assert.Equal(RuleState.Alert, rule.State);
    Assert.Equal(2000, rule.Threshold);
  }
}
