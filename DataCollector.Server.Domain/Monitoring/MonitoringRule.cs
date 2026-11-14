using System.Globalization;
using DataCollector.Server.Domain.Common;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.Domain.Monitoring;

/// <summary>
/// Правило моніторингу: показник (кількість, сума, середнє, мінімум, максимум) за період на записах моделі,
/// що відповідають умовам, порівнюється з порогом. Поріг - або фіксоване число (<see cref="Threshold"/>), або інший
/// показник, теж порахований з даних (<see cref="Comparison"/>): тієї самої чи іншої моделі, за той самий чи інший
/// період, помножений на коефіцієнт. Фоновий сервіс періодично рахує показники і передає результат
/// у <see cref="ApplyCheck"/>, який вирішує, чи змінився стан і чи потрібне сповіщення.
/// </summary>
/// <remarks>
/// «Немає нових записів за 2 години» - кількість записів за 120 хв &lt; 1.
/// «Продажі цього місяця більші, ніж минулого» - сума за поточний місяць &gt; сума за попередній місяць.
/// </remarks>
public sealed class MonitoringRule
{
  public const int MaxConsecutiveChecks = 10;
  public const int MinCheckIntervalSeconds = 30;
  public const int MaxCheckIntervalSeconds = 86_400;
  public const int MaxWindowMinutes = RulePeriod.MaxMinutes;
  public const int MaxSearchLength = 200;
  public const int MaxErrorLength = 1000;

  private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

  public Guid Id { get; }
  public DateTime CreatedAt { get; }

  // ---- Визначення ----
  public Guid ConfigId { get; private set; }
  public string Name { get; private set; } = null!;
  public bool IsEnabled { get; private set; }

  /// <summary>Умови у форматі API (JSON-масив, як filters у POST .../records/query); null - без умов.</summary>
  public string? Filters { get; private set; }

  public string? Search { get; private set; }
  public AggregateFunction Function { get; private set; }
  public string? FieldKey { get; private set; }
  public string TimeField { get; private set; } = RecordField.CreatedAtKey;

  /// <summary>Ковзне вікно часу в хвилинах; null - календарний період (<see cref="WindowUnit"/>) або усі записи.</summary>
  public int? WindowMinutes { get; private set; }

  /// <summary>Календарний період (поточний день, місяць...); null - ковзне вікно.</summary>
  public DateInterval? WindowUnit { get; private set; }

  /// <summary>На скільки періодів назад зсунуто вікно (0 - поточне).</summary>
  public int WindowOffset { get; private set; }

  /// <summary>Зсунутий календарний період - лише до того ж моменту, що й поточний.</summary>
  public bool WindowToDate { get; private set; }

  /// <summary>Часовий пояс календарних періодів (межі дня, місяця); null - UTC.</summary>
  public string? TimeZone { get; private set; }

  public RuleOperator Operator { get; private set; }

  /// <summary>Фіксований поріг; не використовується, якщо задано <see cref="Comparison"/>.</summary>
  public decimal Threshold { get; private set; }

  /// <summary>Показник, з яким порівнюється значення правила замість фіксованого порогу; null - фіксований поріг.</summary>
  public RuleComparison? Comparison { get; private set; }
  public int ConsecutiveChecks { get; private set; }
  public int CheckIntervalSeconds { get; private set; }
  public DateTime? UpdatedAt { get; private set; }

  /// <summary>Номер визначення: результат перевірки за старим визначенням не записується.</summary>
  public int Version { get; private set; }

  // ---- Стан ----
  public RuleState State { get; private set; }
  public DateTime? StateSince { get; private set; }
  public decimal? LastValue { get; private set; }

  /// <summary>Значення показника порівняння на останній перевірці (без коефіцієнта).</summary>
  public decimal? LastCompareValue { get; private set; }

  public DateTime? LastCheckedAt { get; private set; }
  public string? LastError { get; private set; }
  public RuleState? PendingState { get; private set; }
  public int PendingCount { get; private set; }
  public DateTime NextCheckAt { get; private set; }

  private MonitoringRule(Guid id, DateTime createdAt)
  {
    Id = id;
    CreatedAt = createdAt;
  }

  /// <summary>Поточне вікно правила.</summary>
  public RulePeriod Period => new(WindowMinutes, WindowUnit, WindowOffset, WindowToDate);

  /// <param name="compareConfig">Модель показника порівняння (якщо <see cref="RuleDefinition.Comparison"/> задано).</param>
  public static MonitoringRule Create(RuleDefinition definition, EntityConfig config, EntityConfig? compareConfig = null)
  {
    var now = DateTime.UtcNow;
    var rule = new MonitoringRule(Guid.NewGuid(), now) { Version = 1, NextCheckAt = now };
    rule.Apply(definition, config, compareConfig);
    return rule;
  }

  /// <summary>Відновлення з БД (без перевірок - дані вже пройшли їх при збереженні).</summary>
  public static MonitoringRule Restore(Guid id, DateTime createdAt, RuleDefinition definition, Guid configId, RuleSnapshot state)
  {
    return new MonitoringRule(id, createdAt)
    {
      ConfigId = configId,
      Name = definition.Name,
      IsEnabled = definition.IsEnabled,
      Filters = definition.Filters,
      Search = definition.Search,
      Function = definition.Function,
      FieldKey = definition.FieldKey,
      TimeField = definition.TimeField,
      WindowMinutes = definition.WindowMinutes,
      WindowUnit = definition.WindowUnit,
      WindowOffset = definition.WindowOffset,
      WindowToDate = definition.WindowToDate,
      TimeZone = definition.TimeZone,
      Operator = definition.Operator,
      Threshold = definition.Threshold,
      Comparison = definition.Comparison,
      ConsecutiveChecks = definition.ConsecutiveChecks,
      CheckIntervalSeconds = definition.CheckIntervalSeconds,
      State = state.State,
      StateSince = state.StateSince,
      LastValue = state.LastValue,
      LastCompareValue = state.LastCompareValue,
      LastCheckedAt = state.LastCheckedAt,
      LastError = state.LastError,
      PendingState = state.PendingState,
      PendingCount = state.PendingCount,
      NextCheckAt = state.NextCheckAt,
      Version = state.Version,
      UpdatedAt = state.UpdatedAt
    };
  }

  /// <summary>Нове визначення (редагування). Стан зберігається; інша модель - стан починається заново.</summary>
  public void Change(RuleDefinition definition, EntityConfig config, EntityConfig? compareConfig = null)
  {
    if (config.Id != ConfigId)
    {
      State = RuleState.Unknown;
      StateSince = null;
      LastValue = null;
      LastCompareValue = null;
    }

    Apply(definition, config, compareConfig);
    if (Comparison is null)
      LastCompareValue = null;

    PendingState = null;
    PendingCount = 0;
    Version++;
    UpdatedAt = DateTime.UtcNow;
    NextCheckAt = UpdatedAt.Value;
  }

  private void Apply(RuleDefinition d, EntityConfig config, EntityConfig? compareConfig)
  {
    ConfigId = config.Id;
    Name = Guard.Name(d.Name, "правила");
    IsEnabled = d.IsEnabled;
    Filters = string.IsNullOrWhiteSpace(d.Filters) ? null : d.Filters;

    Search = string.IsNullOrWhiteSpace(d.Search) ? null : d.Search.Trim();
    if (Search?.Length > MaxSearchLength)
      throw new DomainValidationException($"Пошук у правилі - не довше {MaxSearchLength} символів");

    // Показник і поле часу перевіряються тими самими правилами, що в агрегації.
    Function = d.Function;
    FieldKey = string.IsNullOrWhiteSpace(d.FieldKey) ? null : d.FieldKey.Trim();
    var metric = Metric(config);
    if (metric.Field is { } field && field.SystemField is null && field.Type != PropertyDataType.Number)
      throw new DomainValidationException($"Поле показника '{field.Name}' має бути числом (або показник - кількість)");

    var period = new RulePeriod(d.WindowMinutes, d.WindowUnit, d.WindowOffset, d.WindowToDate).Validate("Вікно часу");
    WindowMinutes = period.Minutes;
    WindowUnit = period.Unit;
    WindowOffset = period.Offset;
    WindowToDate = period.ToDate;
    TimeField = TimeFieldKey(config, d.TimeField);

    // Часовий пояс перевіряється одразу: невідомий пояс - помилка збереження, а не «немає даних» на кожній перевірці.
    TimeZone = string.IsNullOrWhiteSpace(d.TimeZone) ? null : d.TimeZone.Trim();
    _ = AggregateQuery.ResolveTimeZone(TimeZone);

    Operator = d.Operator;
    Threshold = d.Threshold;
    Comparison = d.Comparison is null ? null : ValidateComparison(d.Comparison, compareConfig);

    if (d.ConsecutiveChecks is < 1 or > MaxConsecutiveChecks)
      throw new DomainValidationException($"Перевірок поспіль - від 1 до {MaxConsecutiveChecks}");

    if (d.CheckIntervalSeconds is < MinCheckIntervalSeconds or > MaxCheckIntervalSeconds)
      throw new DomainValidationException("Інтервал перевірки - від 30 секунд до доби");

    ConsecutiveChecks = d.ConsecutiveChecks;
    CheckIntervalSeconds = d.CheckIntervalSeconds;
  }

  private static RuleComparison ValidateComparison(RuleComparison c, EntityConfig? config)
  {
    if (config is null || config.Id != c.ConfigId)
      throw new DomainValidationException("Модель показника порівняння не знайдена");

    var search = string.IsNullOrWhiteSpace(c.Search) ? null : c.Search.Trim();
    if (search?.Length > MaxSearchLength)
      throw new DomainValidationException($"Пошук у показнику порівняння - не довше {MaxSearchLength} символів");

    var fieldKey = string.IsNullOrWhiteSpace(c.FieldKey) ? null : c.FieldKey.Trim();
    var metric = AggregateMetric.Create(c.Function, fieldKey is null ? null : RecordField.Resolve(config, fieldKey));
    if (metric.Field is { } field && field.SystemField is null && field.Type != PropertyDataType.Number)
      throw new DomainValidationException($"Поле показника порівняння '{field.Name}' має бути числом (або показник - кількість)");

    if (c.Multiplier <= 0 || c.Multiplier > RuleComparison.MaxMultiplier)
      throw new DomainValidationException($"Коефіцієнт порівняння - більше 0 і не більше {RuleComparison.MaxMultiplier:#,0}");

    return c with
    {
      Filters = string.IsNullOrWhiteSpace(c.Filters) ? null : c.Filters,
      Search = search,
      FieldKey = metric.Field?.Property?.Key ?? fieldKey,
      TimeField = TimeFieldKey(config, c.TimeField),
      Period = c.Period.Validate("Період показника порівняння")
    };
  }

  /// <summary>Поле часу періоду: дата створення / зміни запису або поле «дата і час».</summary>
  private static string TimeFieldKey(EntityConfig config, string? key)
  {
    key = string.IsNullOrWhiteSpace(key) ? RecordField.CreatedAtKey : key.Trim();
    var time = RecordField.Resolve(config, key);
    if (time.SystemField is null && time.Type != PropertyDataType.DateTime)
      throw new DomainValidationException($"Поле часу '{time.Name}' має бути датою з часом або датою створення / зміни запису");

    return time.Property?.Key ?? key;
  }

  /// <summary>Показник правила для агрегації.</summary>
  public AggregateMetric Metric(EntityConfig config)
    => AggregateMetric.Create(Function, FieldKey is null ? null : RecordField.Resolve(config, FieldKey));

  /// <summary>Часовий пояс календарних періодів.</summary>
  public TimeZoneInfo? Zone => AggregateQuery.ResolveTimeZone(TimeZone);

  /// <summary>Умови вікна часу за полем часу (0-2 умови: від і, для зсунутого вікна, до).</summary>
  public List<RecordFilter> WindowFilters(EntityConfig config, DateTime nowUtc)
    => Period.Filters(RecordField.Resolve(config, TimeField), nowUtc, Zone);

  /// <summary>Поріг на цій перевірці: фіксований або показник порівняння × коефіцієнт; null - порівнювати нема з чим.</summary>
  public decimal? EffectiveThreshold(decimal? compareValue)
    => Comparison is null ? Threshold : compareValue is { } v ? Math.Round(v * Comparison.Multiplier, 10) : null;

  /// <summary>Стан, який показав би результат перевірки (без урахування перевірок поспіль).</summary>
  public RuleState Observe(decimal? value, string? error, decimal? compareValue)
    => error != null || value is null || EffectiveThreshold(compareValue) is not { } threshold
      ? RuleState.NoData
      : Operator.Matches(value.Value, threshold) ? RuleState.Alert : RuleState.Ok;

  public void Enable(bool enabled)
  {
    if (IsEnabled == enabled)
      return;

    IsEnabled = enabled;
    PendingState = null;
    PendingCount = 0;
    Version++;
    UpdatedAt = DateTime.UtcNow;

    // Увімкнули - перевірити одразу.
    if (enabled)
      NextCheckAt = UpdatedAt.Value;
  }

  /// <summary>
  /// Результат перевірки -> новий стан. Стан змінюється, лише коли новий результат повторився
  /// <see cref="ConsecutiveChecks"/> разів поспіль; перша перевірка встановлює стан одразу.
  /// </summary>
  /// <param name="value">Значення показника; null - порахувати не вдалося (немає записів або помилка).</param>
  /// <param name="error">Помилка розрахунку (поле видалено тощо) - стан «немає даних» з поясненням.</param>
  /// <param name="metricName">Назва показника для тексту сповіщення («Середнє: Температура»).</param>
  /// <param name="compareValue">Значення показника порівняння (якщо поріг - показник); null - немає даних для порівняння.</param>
  /// <param name="compareName">Назва показника порівняння для тексту сповіщення.</param>
  /// <returns>Сповіщення про зміну стану або null, якщо стан не змінився (або перша перевірка показала норму).</returns>
  public NotificationDraft? ApplyCheck(
    decimal? value, string? error, DateTime nowUtc, string metricName, decimal? compareValue = null, string? compareName = null)
  {
    var observed = Observe(value, error, compareValue);
    var threshold = EffectiveThreshold(compareValue);

    // Показник є, а порівняти нема з чим - пояснення, щоб «немає даних» не виглядало як брак записів у вікні.
    if (error is null && value is not null && threshold is null && Comparison is not null)
      error = $"немає даних для порівняння: {compareName ?? "показник порівняння"} {Comparison.Period.Text()}";

    LastValue = value;
    LastCompareValue = Comparison is null ? null : compareValue;
    LastCheckedAt = nowUtc;
    LastError = error is null ? null : Truncate(error, MaxErrorLength);

    if (observed == State)
    {
      PendingState = null;
      PendingCount = 0;
      return null;
    }

    // Перша перевірка - стан встановлюється одразу (чекати N перевірок немає з чим порівнювати).
    if (State != RuleState.Unknown)
    {
      PendingCount = PendingState == observed ? PendingCount + 1 : 1;
      PendingState = observed;

      if (PendingCount < ConsecutiveChecks)
        return null;
    }

    var from = State;
    State = observed;
    StateSince = nowUtc;
    PendingState = null;
    PendingCount = 0;

    // Перша перевірка показала норму - повідомляти нема про що.
    if (from == RuleState.Unknown && observed == RuleState.Ok)
      return null;

    return new NotificationDraft(Guid.NewGuid(), from, observed, value, threshold ?? Threshold,
      Message(from, observed, value, metricName, threshold, compareName));
  }

  /// <summary>
  /// Умова правила текстом: «Середнє: Температура > 30 за 15 хв»,
  /// «Сума: Сума за поточний місяць > Сума: Сума за попередній місяць × 1,1».
  /// </summary>
  public string Describe(string metricName, string? compareName = null)
    => Comparison is null
      ? $"{metricName} {Operator.Symbol()} {Number(Threshold)} {WindowText()}"
      : $"{metricName} {WindowText()} {Operator.Symbol()} {CompareText(compareName)}";

  public string WindowText() => Period.Text();

  /// <summary>Показник порівняння текстом: «Сума: Сума за попередній місяць × 1,1».</summary>
  private string CompareText(string? compareName)
  {
    var c = Comparison!;
    var multiplier = c.Multiplier == 1 ? "" : $" × {Number(c.Multiplier)}";
    return $"{compareName ?? "показник порівняння"} {c.Period.Text()}{multiplier}";
  }

  private string Message(RuleState from, RuleState to, decimal? value, string metricName, decimal? threshold, string? compareName)
  {
    var current = value is { } v ? $"{metricName} = {Number(v)}" : null;
    var condition = Comparison is null
      ? $"{Operator.Symbol()} {Number(Threshold)} {WindowText()}"
      : $"{WindowText()} {Operator.Symbol()} {CompareText(compareName)}{(threshold is { } t ? $" = {Number(t)}" : "")}";

    return to switch
    {
      RuleState.Alert => $"«{Name}»: спрацювало - {current} (умова: {condition})",
      RuleState.Ok => from == RuleState.NoData
        ? $"«{Name}»: дані знову є, норма - {current}"
        : $"«{Name}»: знову норма - {current}",
      _ => $"«{Name}»: немає даних - {LastError ?? $"немає записів для розрахунку {WindowText()}"}"
    };
  }

  private static string Number(decimal value)
    => (value / 1.0000000000000000000000000000m).ToString("#,0.##########", Ukrainian);

  private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}

/// <summary>Що задає користувач у правилі (вхід для створення / зміни).</summary>
public sealed record RuleDefinition(
  string Name,
  bool IsEnabled,
  string? Filters,
  string? Search,
  AggregateFunction Function,
  string? FieldKey,
  string TimeField,
  int? WindowMinutes,
  RuleOperator Operator,
  decimal Threshold,
  int ConsecutiveChecks,
  int CheckIntervalSeconds,
  DateInterval? WindowUnit = null,
  int WindowOffset = 0,
  bool WindowToDate = false,
  string? TimeZone = null,
  RuleComparison? Comparison = null);

/// <summary>
/// Показник, з яким порівнюється значення правила замість фіксованого порогу: з будь-якої моделі, зі своїми умовами,
/// функцією і періодом. Порогом стає значення × <see cref="Multiplier"/> (1,1 - «більше на 10%»).
/// </summary>
public sealed record RuleComparison(
  Guid ConfigId,
  string? Filters,
  string? Search,
  AggregateFunction Function,
  string? FieldKey,
  string TimeField,
  RulePeriod Period,
  decimal Multiplier = 1)
{
  public const decimal MaxMultiplier = 1_000_000m;

  public AggregateMetric Metric(EntityConfig config)
    => AggregateMetric.Create(Function, FieldKey is null ? null : RecordField.Resolve(config, FieldKey));

  /// <summary>Умови періоду порівняння за його полем часу.</summary>
  public List<RecordFilter> PeriodFilters(EntityConfig config, DateTime nowUtc, TimeZoneInfo? timeZone)
    => Period.Filters(RecordField.Resolve(config, TimeField), nowUtc, timeZone);
}

/// <summary>Збережений стан правила (з БД).</summary>
public sealed record RuleSnapshot(
  RuleState State,
  DateTime? StateSince,
  decimal? LastValue,
  DateTime? LastCheckedAt,
  string? LastError,
  RuleState? PendingState,
  int PendingCount,
  DateTime NextCheckAt,
  int Version,
  DateTime? UpdatedAt,
  decimal? LastCompareValue = null);

/// <summary>Сповіщення про зміну стану, яке треба записати разом зі станом правила.</summary>
public sealed record NotificationDraft(
  Guid Id,
  RuleState FromState,
  RuleState ToState,
  decimal? Value,
  decimal Threshold,
  string Message);
