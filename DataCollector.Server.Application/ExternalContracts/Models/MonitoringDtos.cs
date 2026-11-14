namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Створення або зміна правила моніторингу. Умови й пошук - як у POST .../records/query.
/// Поріг - фіксоване число (threshold) або інший показник з даних (compare).
/// </summary>
/// <example>
/// { "config": "…", "name": "Перегрів складу", "function": "avg", "field": "temperature",
///   "windowMinutes": 15, "operator": "gt", "threshold": 30, "checkIntervalSeconds": 60 }
///
/// { "config": "orders", "name": "Продажі ростуть", "function": "sum", "field": "amount",
///   "windowUnit": "month", "operator": "gt", "timeZone": "Europe/Kyiv",
///   "compare": { "config": "orders", "function": "sum", "field": "amount",
///                "windowUnit": "month", "windowOffset": 1, "windowToDate": true, "multiplier": 1.1 } }
/// </example>
public class SaveMonitoringRuleCommand
{
  /// <summary>Id або ключ моделі.</summary>
  public string Config { get; set; } = null!;

  public string Name { get; set; } = null!;
  public bool IsEnabled { get; set; } = true;
  public List<RecordFilterDto>? Filters { get; set; }
  public string? Search { get; set; }

  /// <summary>"count", "sum", "avg", "min", "max".</summary>
  public string Function { get; set; } = "count";

  /// <summary>Поле показника (число); для count - необов'язкове.</summary>
  public string? Field { get; set; }

  /// <summary>"createdAt" (за замовчуванням), "updatedAt" або поле моделі «дата і час».</summary>
  public string? TimeField { get; set; }

  /// <summary>Ковзне вікно часу, хвилин; null - календарний період (windowUnit) або усі записи.</summary>
  public int? WindowMinutes { get; set; }

  /// <summary>Календарний період: "hour", "day", "week", "month", "quarter", "year"; null - ковзне вікно.</summary>
  public string? WindowUnit { get; set; }

  /// <summary>На скільки періодів назад зсунути вікно: 0 - поточне, 1 - попереднє.</summary>
  public int WindowOffset { get; set; }

  /// <summary>Зсунутий календарний період - лише до того ж моменту, що й поточний.</summary>
  public bool WindowToDate { get; set; }

  /// <summary>Часовий пояс календарних періодів (IANA або Windows, напр. "Europe/Kyiv"); null - UTC.</summary>
  public string? TimeZone { get; set; }

  /// <summary>"gt", "gte", "lt", "lte", "eq", "neq".</summary>
  public string Operator { get; set; } = "gt";

  /// <summary>Фіксований поріг (якщо compare не задано).</summary>
  public decimal Threshold { get; set; }

  /// <summary>Показник, з яким порівнювати замість фіксованого порогу; null - поріг threshold.</summary>
  public MonitoringCompareCommand? Compare { get; set; }
  public int ConsecutiveChecks { get; set; } = 1;
  public int CheckIntervalSeconds { get; set; } = 60;

  /// <summary>
  /// Версія правила, яку бачив редактор (лише для зміни). Не збігається з поточною - 409. null - без перевірки.
  /// </summary>
  public int? Version { get; set; }
}

/// <summary>
/// Показник порівняння: рахується так само, як показник правила (модель, умови, функція, період), і, помножений
/// на коефіцієнт, стає порогом.
/// </summary>
public class MonitoringCompareCommand
{
  /// <summary>Id або ключ моделі (може відрізнятися від моделі правила).</summary>
  public string Config { get; set; } = null!;

  public List<RecordFilterDto>? Filters { get; set; }
  public string? Search { get; set; }

  /// <summary>"count", "sum", "avg", "min", "max".</summary>
  public string Function { get; set; } = "count";

  public string? Field { get; set; }
  public string? TimeField { get; set; }
  public int? WindowMinutes { get; set; }
  public string? WindowUnit { get; set; }
  public int WindowOffset { get; set; }
  public bool WindowToDate { get; set; }

  /// <summary>Коефіцієнт: поріг = значення × коефіцієнт (1,1 - на 10% більше; 0,8 - на 20% менше).</summary>
  public decimal Multiplier { get; set; } = 1;
}

public class MonitoringCompareDto
{
  public Guid ConfigId { get; set; }
  public string ConfigName { get; set; } = null!;
  public List<RecordFilterDto> Filters { get; set; } = [];
  public string? Search { get; set; }
  public string Function { get; set; } = null!;
  public string? Field { get; set; }
  public string TimeField { get; set; } = null!;
  public int? WindowMinutes { get; set; }
  public string? WindowUnit { get; set; }
  public int WindowOffset { get; set; }
  public bool WindowToDate { get; set; }
  public decimal Multiplier { get; set; }
}

public class MonitoringRuleDto
{
  public Guid Id { get; set; }
  public Guid ConfigId { get; set; }
  public string ConfigName { get; set; } = null!;
  public string Name { get; set; } = null!;
  public bool IsEnabled { get; set; }
  public List<RecordFilterDto> Filters { get; set; } = [];
  public string? Search { get; set; }
  public string Function { get; set; } = null!;
  public string? Field { get; set; }
  public string TimeField { get; set; } = null!;
  public int? WindowMinutes { get; set; }
  public string? WindowUnit { get; set; }
  public int WindowOffset { get; set; }
  public bool WindowToDate { get; set; }
  public string? TimeZone { get; set; }
  public string Operator { get; set; } = null!;
  public decimal Threshold { get; set; }

  /// <summary>Показник порівняння; null - фіксований поріг.</summary>
  public MonitoringCompareDto? Compare { get; set; }

  public int ConsecutiveChecks { get; set; }
  public int CheckIntervalSeconds { get; set; }

  /// <summary>Умова текстом: «Середнє: Температура > 30 за 15 хв».</summary>
  public string Condition { get; set; } = null!;

  /// <summary>"unknown", "ok", "alert", "noData".</summary>
  public string State { get; set; } = null!;
  public DateTime? StateSince { get; set; }
  public decimal? LastValue { get; set; }

  /// <summary>Значення показника порівняння на останній перевірці.</summary>
  public decimal? LastCompareValue { get; set; }

  /// <summary>Поріг на останній перевірці: фіксований або показник порівняння × коефіцієнт.</summary>
  public decimal? LastThreshold { get; set; }

  public DateTime? LastCheckedAt { get; set; }
  public string? LastError { get; set; }
  public DateTime NextCheckAt { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }

  /// <summary>Версія визначення (оптимістична конкурентність): передається назад при зміні.</summary>
  public int Version { get; set; }
}

/// <summary>Розрахунок правила «зараз» без збереження - для перевірки умови в редакторі.</summary>
public class MonitoringPreviewDto
{
  public decimal? Value { get; set; }

  /// <summary>Значення показника порівняння і поріг, що з нього вийшов (якщо поріг - показник).</summary>
  public decimal? CompareValue { get; set; }
  public string? CompareName { get; set; }
  public decimal? Threshold { get; set; }

  public string State { get; set; } = null!;
  public string MetricName { get; set; } = null!;
  public string Condition { get; set; } = null!;
  public string? Error { get; set; }
}

public class NotificationDto
{
  public Guid Id { get; set; }
  public Guid RuleId { get; set; }
  public string RuleName { get; set; } = null!;
  public string ConfigName { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public string FromState { get; set; } = null!;
  public string ToState { get; set; } = null!;
  public decimal? Value { get; set; }
  public decimal Threshold { get; set; }
  public string Message { get; set; } = null!;
  public DateTime? ReadAt { get; set; }

  /// <summary>"notRequired", "pending", "sent", "failed".</summary>
  public string Delivery { get; set; } = null!;
  public int DeliveryAttempts { get; set; }
  public DateTime? DeliveredAt { get; set; }
  public string? DeliveryError { get; set; }
}

public class UnreadCountDto
{
  public int Count { get; set; }
}
