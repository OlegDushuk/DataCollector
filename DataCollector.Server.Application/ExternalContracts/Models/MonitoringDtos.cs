namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Створення або зміна правила моніторингу. Умови й пошук - як у POST .../records/query.
/// </summary>
/// <example>
/// { "configId": "…", "name": "Перегрів складу", "function": "avg", "field": "temperature",
///   "windowMinutes": 15, "operator": "gt", "threshold": 30, "checkIntervalSeconds": 60 }
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

  /// <summary>Вікно часу, хвилин; null - усі записи.</summary>
  public int? WindowMinutes { get; set; }

  /// <summary>"gt", "gte", "lt", "lte", "eq", "neq".</summary>
  public string Operator { get; set; } = "gt";

  public decimal Threshold { get; set; }
  public int ConsecutiveChecks { get; set; } = 1;
  public int CheckIntervalSeconds { get; set; } = 60;
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
  public string Operator { get; set; } = null!;
  public decimal Threshold { get; set; }
  public int ConsecutiveChecks { get; set; }
  public int CheckIntervalSeconds { get; set; }

  /// <summary>Умова текстом: «Середнє: Температура > 30 за 15 хв».</summary>
  public string Condition { get; set; } = null!;

  /// <summary>"unknown", "ok", "alert", "noData".</summary>
  public string State { get; set; } = null!;
  public DateTime? StateSince { get; set; }
  public decimal? LastValue { get; set; }
  public DateTime? LastCheckedAt { get; set; }
  public string? LastError { get; set; }
  public DateTime NextCheckAt { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
}

/// <summary>Розрахунок правила «зараз» без збереження - для перевірки умови в редакторі.</summary>
public class MonitoringPreviewDto
{
  public decimal? Value { get; set; }
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
