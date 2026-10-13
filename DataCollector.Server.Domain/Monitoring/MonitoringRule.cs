using System.Globalization;
using DataCollector.Server.Domain.Common;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.Domain.Monitoring;

/// <summary>
/// Правило моніторингу: показник (кількість, сума, середнє, мінімум, максимум) за вікно часу на записах моделі,
/// що відповідають умовам, порівнюється з порогом. Фоновий сервіс періодично рахує показник і передає результат
/// у <see cref="ApplyCheck"/>, який вирішує, чи змінився стан і чи потрібне сповіщення.
/// </summary>
/// <remarks>
/// «Немає нових записів за 2 години» - це те саме правило: кількість записів за 120 хв &lt; 1.
/// </remarks>
public sealed class MonitoringRule
{
  public const int MaxConsecutiveChecks = 10;
  public const int MinCheckIntervalSeconds = 30;
  public const int MaxCheckIntervalSeconds = 86_400;
  public const int MaxWindowMinutes = 525_600;
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

  /// <summary>Вікно часу в хвилинах; null - усі записи.</summary>
  public int? WindowMinutes { get; private set; }

  public RuleOperator Operator { get; private set; }
  public decimal Threshold { get; private set; }
  public int ConsecutiveChecks { get; private set; }
  public int CheckIntervalSeconds { get; private set; }
  public DateTime? UpdatedAt { get; private set; }

  /// <summary>Номер визначення: результат перевірки за старим визначенням не записується.</summary>
  public int Version { get; private set; }

  // ---- Стан ----
  public RuleState State { get; private set; }
  public DateTime? StateSince { get; private set; }
  public decimal? LastValue { get; private set; }
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

  public static MonitoringRule Create(RuleDefinition definition, EntityConfig config)
  {
    var now = DateTime.UtcNow;
    var rule = new MonitoringRule(Guid.NewGuid(), now) { Version = 1, NextCheckAt = now };
    rule.Apply(definition, config);
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
      Operator = definition.Operator,
      Threshold = definition.Threshold,
      ConsecutiveChecks = definition.ConsecutiveChecks,
      CheckIntervalSeconds = definition.CheckIntervalSeconds,
      State = state.State,
      StateSince = state.StateSince,
      LastValue = state.LastValue,
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
  public void Change(RuleDefinition definition, EntityConfig config)
  {
    if (config.Id != ConfigId)
    {
      State = RuleState.Unknown;
      StateSince = null;
      LastValue = null;
    }

    Apply(definition, config);
    PendingState = null;
    PendingCount = 0;
    Version++;
    UpdatedAt = DateTime.UtcNow;
    NextCheckAt = UpdatedAt.Value;
  }

  private void Apply(RuleDefinition d, EntityConfig config)
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

    if (d.WindowMinutes is { } window && (window < 1 || window > MaxWindowMinutes))
      throw new DomainValidationException($"Вікно часу - від 1 хвилини до {MaxWindowMinutes / 1440} днів");

    WindowMinutes = d.WindowMinutes;
    TimeField = string.IsNullOrWhiteSpace(d.TimeField) ? RecordField.CreatedAtKey : d.TimeField.Trim();
    var time = RecordField.Resolve(config, TimeField);
    if (time.SystemField is null && time.Type != PropertyDataType.DateTime)
      throw new DomainValidationException($"Поле часу '{time.Name}' має бути датою з часом або датою створення / зміни запису");

    TimeField = time.Property?.Key ?? TimeField;

    Operator = d.Operator;
    Threshold = d.Threshold;

    if (d.ConsecutiveChecks is < 1 or > MaxConsecutiveChecks)
      throw new DomainValidationException($"Перевірок поспіль - від 1 до {MaxConsecutiveChecks}");

    if (d.CheckIntervalSeconds is < MinCheckIntervalSeconds or > MaxCheckIntervalSeconds)
      throw new DomainValidationException("Інтервал перевірки - від 30 секунд до доби");

    ConsecutiveChecks = d.ConsecutiveChecks;
    CheckIntervalSeconds = d.CheckIntervalSeconds;
  }

  /// <summary>Показник правила для агрегації.</summary>
  public AggregateMetric Metric(EntityConfig config)
    => AggregateMetric.Create(Function, FieldKey is null ? null : RecordField.Resolve(config, FieldKey));

  /// <summary>Умова вікна часу: поле часу ≥ now - вікно; null - без вікна.</summary>
  public RecordFilter? WindowFilter(EntityConfig config, DateTime nowUtc)
  {
    if (WindowMinutes is not { } minutes)
      return null;

    var from = nowUtc.AddMinutes(-minutes).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    return RecordFilter.Create(RecordField.Resolve(config, TimeField), FilterOperator.GreaterOrEqual, from);
  }

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
  /// <returns>Сповіщення про зміну стану або null, якщо стан не змінився (або перша перевірка показала норму).</returns>
  public NotificationDraft? ApplyCheck(decimal? value, string? error, DateTime nowUtc, string metricName)
  {
    var observed = error != null || value is null
      ? RuleState.NoData
      : Operator.Matches(value.Value, Threshold) ? RuleState.Alert : RuleState.Ok;

    LastValue = value;
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

    return new NotificationDraft(Guid.NewGuid(), from, observed, value, Threshold, Message(from, observed, value, metricName));
  }

  /// <summary>Умова правила текстом: «Середнє: Температура > 30 за 15 хв».</summary>
  public string Describe(string metricName)
    => $"{metricName} {Operator.Symbol()} {Number(Threshold)} {WindowText()}";

  public string WindowText() => WindowMinutes switch
  {
    null => "за весь час",
    var m when m % 1440 == 0 => $"за {m / 1440} дн.",
    var m when m % 60 == 0 => $"за {m / 60} год",
    var m => $"за {m} хв"
  };

  private string Message(RuleState from, RuleState to, decimal? value, string metricName)
  {
    var current = value is { } v ? $"{metricName} = {Number(v)}" : null;

    return to switch
    {
      RuleState.Alert => $"«{Name}»: спрацювало - {current} (умова: {Operator.Symbol()} {Number(Threshold)} {WindowText()})",
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
  int CheckIntervalSeconds);

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
  DateTime? UpdatedAt);

/// <summary>Сповіщення про зміну стану, яке треба записати разом зі станом правила.</summary>
public sealed record NotificationDraft(
  Guid Id,
  RuleState FromState,
  RuleState ToState,
  decimal? Value,
  decimal Threshold,
  string Message);
