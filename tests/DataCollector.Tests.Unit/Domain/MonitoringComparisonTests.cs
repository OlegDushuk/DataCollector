using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Monitoring;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Tests.Unit.Domain;

/// <summary>Періоди правила (ковзні й календарні, зі зсувом) і порівняння з іншим показником замість фіксованого порогу.</summary>
public class MonitoringComparisonTests
{
  // Четвер, 15 жовтня 2026, 12:00 UTC (15:00 за Києвом / Гельсінкі, UTC+3).
  private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

  // Той самий пояс, що й Київ (EET/EEST); у мінімальній базі поясів контейнера Europe/Kyiv може не бути.
  private static readonly TimeZoneInfo Eet = TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki");

  private readonly Server.Domain.Entities.EntityConfig _orders = TestModels.Orders();
  private readonly Server.Domain.Entities.EntityConfig _returns = TestModels.Orders();

  private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0) => new(y, m, d, h, min, 0, DateTimeKind.Utc);

  // ---------- Періоди ----------

  [Fact]
  public void Sliding_CurrentWindow_IsOpenToTheRight()
  {
    var (from, to) = new RulePeriod(60, null).Range(Now, null);

    Assert.Equal(Now.AddMinutes(-60), from);
    Assert.Null(to);
  }

  [Fact]
  public void Sliding_PreviousWindow_EndsWhereCurrentStarts()
  {
    var (from, to) = new RulePeriod(60, null, Offset: 1).Range(Now, null);

    Assert.Equal(Now.AddMinutes(-120), from);
    Assert.Equal(Now.AddMinutes(-60), to);
  }

  [Fact]
  public void Calendar_CurrentMonth_InTimeZone()
  {
    var (from, to) = new RulePeriod(null, DateInterval.Month).Range(Now, Eet);

    // 1 жовтня 00:00 за київським часом (UTC+3) ... 1 листопада 00:00 (після переходу на зимовий час - UTC+2).
    Assert.Equal(Utc(2026, 9, 30, 21), from);
    Assert.Equal(Utc(2026, 10, 31, 22), to);
  }

  [Fact]
  public void Calendar_PreviousMonthToDate_EndsAtSameMoment()
  {
    var (from, to) = new RulePeriod(null, DateInterval.Month, Offset: 1, ToDate: true).Range(Now, Eet);

    Assert.Equal(Utc(2026, 8, 31, 21), from);
    Assert.Equal(Utc(2026, 9, 15, 12), to);   // 15 вересня 15:00 за Києвом
  }

  [Fact]
  public void Calendar_ToDate_ClampedToShorterMonth()
  {
    // 31 березня: «до того ж моменту» в лютому - це весь лютий.
    var (from, to) = new RulePeriod(null, DateInterval.Month, Offset: 1, ToDate: true).Range(Utc(2026, 3, 31, 10), null);

    Assert.Equal(Utc(2026, 2, 1), from);
    Assert.Equal(Utc(2026, 3, 1), to);
  }

  [Fact]
  public void Calendar_WeekStartsOnMonday_QuarterAndYear()
  {
    Assert.Equal(Utc(2026, 10, 12), new RulePeriod(null, DateInterval.Week).Range(Now, null).From);
    Assert.Equal(Utc(2026, 10, 1), new RulePeriod(null, DateInterval.Quarter).Range(Now, null).From);
    Assert.Equal(Utc(2025, 10, 1), new RulePeriod(null, DateInterval.Month, Offset: 12).Range(Now, null).From);
    Assert.Equal((Utc(2025, 1, 1), Utc(2026, 1, 1)), new RulePeriod(null, DateInterval.Year, Offset: 1).Range(Now, null));
  }

  [Fact]
  public void Period_Validation()
  {
    Assert.Throws<DomainValidationException>(() => new RulePeriod(60, DateInterval.Day).Validate("Вікно"));
    Assert.Throws<DomainValidationException>(() => new RulePeriod(null, null, Offset: 1).Validate("Вікно"));
    Assert.Throws<DomainValidationException>(() => new RulePeriod(0, null).Validate("Вікно"));
    Assert.Throws<DomainValidationException>(() => new RulePeriod(null, DateInterval.Day, Offset: 101).Validate("Вікно"));

    // «До того ж моменту» має сенс лише для зсунутого календарного періоду.
    Assert.False(new RulePeriod(null, DateInterval.Month, ToDate: true).Validate("Вікно").ToDate);
    Assert.False(new RulePeriod(60, null, Offset: 1, ToDate: true).Validate("Вікно").ToDate);
  }

  [Theory]
  [InlineData(null, 0, false, "за поточний місяць")]
  [InlineData(null, 1, true, "за попередній місяць до того ж моменту")]
  [InlineData(null, 12, false, "за місяць, що був 12 місяців тому")]
  [InlineData(1440, 0, false, "за 1 дн.")]
  [InlineData(1440, 1, false, "за попередні 1 дн.")]
  [InlineData(10080, 2, false, "за 7 дн., що закінчилися 14 дн. тому")]
  public void Period_Text(int? minutes, int offset, bool toDate, string expected)
  {
    var unit = minutes is null ? DateInterval.Month : (DateInterval?)null;
    Assert.Equal(expected, new RulePeriod(minutes, unit, offset, toDate).Text());
  }

  [Fact]
  public void Period_Text_Hour_IsFeminine()
  {
    Assert.Equal("за поточну годину", new RulePeriod(null, DateInterval.Hour).Text());
    Assert.Equal("за попередню годину", new RulePeriod(null, DateInterval.Hour, Offset: 1).Text());
  }

  [Fact]
  public void WindowFilters_CalendarPeriod_GivesFromAndTo()
  {
    var rule = MonitoringRule.Create(Definition(unit: DateInterval.Month, offset: 1), _orders);

    var filters = rule.WindowFilters(_orders, Now);

    Assert.Equal(2, filters.Count);
    Assert.Equal(FilterOperator.GreaterOrEqual, filters[0].Operator);
    Assert.Equal(FilterOperator.LessThan, filters[1].Operator);
  }

  [Fact]
  public void Definition_UnknownTimeZone_Rejected()
  {
    Assert.Throws<DomainValidationException>(() => MonitoringRule.Create(Definition(timeZone: "Mars/Olympus"), _orders));
  }

  // ---------- Порівняння з іншим показником ----------

  private RuleDefinition Definition(
    DateInterval? unit = DateInterval.Month,
    int offset = 0,
    string? timeZone = null,
    RuleComparison? comparison = null,
    RuleOperator op = RuleOperator.GreaterThan)
    => new("Продажі ростуть", true, null, null, AggregateFunction.Sum, "price", "createdAt",
      unit is null ? 60 : null, op, 0, 1, 60, unit, offset, false, timeZone, comparison);

  private RuleComparison PreviousMonth(decimal multiplier = 1, Guid? configId = null, string? field = "price")
    => new(configId ?? _orders.Id, null, null, AggregateFunction.Sum, field, "createdAt",
      new RulePeriod(null, DateInterval.Month, 1, true), multiplier);

  [Fact]
  public void Comparison_ThresholdIsCompareValueTimesMultiplier()
  {
    var rule = MonitoringRule.Create(Definition(comparison: PreviousMonth(1.1m)), _orders, _orders);

    var notification = rule.ApplyCheck(120, null, Now, "Сума: Сума", 100, "Сума: Сума");

    Assert.Equal(RuleState.Alert, rule.State);
    Assert.NotNull(notification);
    Assert.Equal(110, notification!.Threshold);
    Assert.Equal(100, rule.LastCompareValue);
    Assert.Contains("× 1,1 = 110", notification.Message);
  }

  [Fact]
  public void Comparison_BelowThreshold_IsOk()
  {
    var rule = MonitoringRule.Create(Definition(comparison: PreviousMonth(1.1m)), _orders, _orders);

    rule.ApplyCheck(105, null, Now, "Сума: Сума", 100, "Сума: Сума");

    Assert.Equal(RuleState.Ok, rule.State);
  }

  [Fact]
  public void Comparison_NoCompareValue_IsNoDataWithReason()
  {
    var rule = MonitoringRule.Create(Definition(comparison: PreviousMonth()), _orders, _orders);

    rule.ApplyCheck(120, null, Now, "Сума: Сума", null, "Сума: Сума");

    Assert.Equal(RuleState.NoData, rule.State);
    Assert.StartsWith("немає даних для порівняння: Сума: Сума за попередній місяць", rule.LastError);
  }

  [Fact]
  public void Comparison_Describe()
  {
    var rule = MonitoringRule.Create(Definition(comparison: PreviousMonth(1.1m)), _orders, _orders);

    Assert.Equal(
      "Сума: Сума за поточний місяць > Сума: Сума за попередній місяць до того ж моменту × 1,1",
      rule.Describe("Сума: Сума", "Сума: Сума"));
  }

  [Fact]
  public void Comparison_FromAnotherModel()
  {
    var rule = MonitoringRule.Create(Definition(comparison: PreviousMonth(configId: _returns.Id)), _orders, _returns);

    Assert.Equal(_returns.Id, rule.Comparison!.ConfigId);
  }

  [Fact]
  public void Comparison_Validation()
  {
    // Модель порівняння не передана або інша.
    Assert.Throws<DomainValidationException>(() => MonitoringRule.Create(Definition(comparison: PreviousMonth()), _orders, _returns));
    // Коефіцієнт має бути додатним.
    Assert.Throws<DomainValidationException>(() => MonitoringRule.Create(Definition(comparison: PreviousMonth(0)), _orders, _orders));
    // Сума - лише числового поля.
    Assert.Throws<DomainValidationException>(() => MonitoringRule.Create(Definition(comparison: PreviousMonth(field: "number")), _orders, _orders));
  }

  [Fact]
  public void Change_ToFixedThreshold_ForgetsCompareValue()
  {
    var rule = MonitoringRule.Create(Definition(comparison: PreviousMonth()), _orders, _orders);
    rule.ApplyCheck(120, null, Now, "Сума", 100, "Сума");

    rule.Change(Definition() with { Threshold = 50 }, _orders);

    Assert.Null(rule.Comparison);
    Assert.Null(rule.LastCompareValue);
    Assert.Equal(50, rule.EffectiveThreshold(null));
  }
}
