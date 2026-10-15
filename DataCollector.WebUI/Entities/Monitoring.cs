using System.Globalization;
using System.Text.Json;
using DataCollector.WebUI.Enums;
using DataCollector.WebUI.Helpers;

namespace DataCollector.WebUI.Entities;

/// <summary>Правило моніторингу з API (визначення + поточний стан).</summary>
public class MonitoringRule
{
  public Guid Id { get; set; }
  public Guid ConfigId { get; set; }
  public string ConfigName { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public bool IsEnabled { get; set; }
  public List<ApiFilter> Filters { get; set; } = [];
  public string? Search { get; set; }
  public string Function { get; set; } = "count";
  public string? Field { get; set; }
  public string TimeField { get; set; } = FilterField.CreatedAtKey;
  public int? WindowMinutes { get; set; }
  public string Operator { get; set; } = "gt";
  public decimal Threshold { get; set; }
  public int ConsecutiveChecks { get; set; }
  public int CheckIntervalSeconds { get; set; }
  public string Condition { get; set; } = string.Empty;
  public string State { get; set; } = RuleStates.Unknown;
  public DateTime? StateSince { get; set; }
  public decimal? LastValue { get; set; }
  public DateTime? LastCheckedAt { get; set; }
  public string? LastError { get; set; }
  public DateTime NextCheckAt { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }

  /// <summary>Версія визначення (оптимістична конкурентність): надсилається назад при збереженні.</summary>
  public int Version { get; set; }

  /// <summary>Стан для показу: вимкнене правило - «Вимкнено», незалежно від останнього стану.</summary>
  public string DisplayState => IsEnabled ? State : RuleStates.Disabled;
}

/// <summary>Умова фільтра у форматі API (RecordFilterDto).</summary>
public class ApiFilter
{
  public string Field { get; set; } = string.Empty;
  public string Operator { get; set; } = string.Empty;
  public JsonElement? Value { get; set; }
  public JsonElement? ValueTo { get; set; }
  public List<JsonElement>? Values { get; set; }

  /// <summary>Умова API -> умова форми фільтрів (як на сторінці записів); null - поля вже немає.</summary>
  public RecordFilterForm? ToForm(IReadOnlyList<FilterField> fields)
  {
    if (fields.FirstOrDefault(f => f.Key == Field) is not { } field)
      return null;

    var form = new RecordFilterForm
    {
      Field = Field,
      Operator = Operator,
      Value = FieldValue.ToForm(field.DataType, FieldValue.Raw(Value)),
      ValueTo = FieldValue.ToForm(field.DataType, FieldValue.Raw(ValueTo)),
      Values = Values?.Select(v => FieldValue.Raw(v)).OfType<string>().ToList() ?? []
    };

    if (field.FindOperator(Operator) != null)
      return form;

    // Правило створено через API з умовою, якої немає в редакторі для цього типу (перелік: eq замість in) -
    // та сама умова іншим оператором; інакше умову не показати.
    var replacement = Operator switch
    {
      "eq" => "in",
      "neq" => "notIn",
      _ => null
    };

    if (replacement is null || field.FindOperator(replacement) is null || form.Value is null)
      return null;

    form.Operator = replacement;
    form.Values = [form.Value];
    form.Value = null;
    return form;
  }
}

public class MonitoringPreview
{
  public decimal? Value { get; set; }
  public string State { get; set; } = RuleStates.Unknown;
  public string MetricName { get; set; } = string.Empty;
  public string Condition { get; set; } = string.Empty;
  public string? Error { get; set; }
}

public class NotificationItem
{
  public Guid Id { get; set; }
  public Guid RuleId { get; set; }
  public string RuleName { get; set; } = string.Empty;
  public string ConfigName { get; set; } = string.Empty;
  public DateTime CreatedAt { get; set; }
  public string FromState { get; set; } = string.Empty;
  public string ToState { get; set; } = string.Empty;
  public decimal? Value { get; set; }
  public decimal Threshold { get; set; }
  public string Message { get; set; } = string.Empty;
  public DateTime? ReadAt { get; set; }
  public string Delivery { get; set; } = "notRequired";
  public int DeliveryAttempts { get; set; }
  public DateTime? DeliveredAt { get; set; }
  public string? DeliveryError { get; set; }
}

/// <summary>Стани правила: коди API, підписи й CSS-модифікатори.</summary>
public static class RuleStates
{
  public const string Unknown = "unknown";
  public const string Ok = "ok";
  public const string Alert = "alert";
  public const string NoData = "noData";

  /// <summary>Не стан API: правило вимкнене.</summary>
  public const string Disabled = "disabled";

  public static string Label(string state) => state switch
  {
    Ok => "Норма",
    Alert => "Спрацювало",
    NoData => "Немає даних",
    Disabled => "Вимкнено",
    _ => "Не перевірялось"
  };

  /// <summary>Модифікатор класу state-pill--…</summary>
  public static string Css(string state) => state switch
  {
    Ok => "ok",
    Alert => "alert",
    NoData => "nodata",
    _ => "muted"
  };
}

/// <summary>Форма редактора правила.</summary>
public sealed class RuleForm
{
  public Guid? ConfigId { get; set; }
  public string Name { get; set; } = string.Empty;
  public bool IsEnabled { get; set; } = true;
  public List<RecordFilterForm> Filters { get; set; } = [];
  public string? Search { get; set; }
  public string Function { get; set; } = "count";
  public string Field { get; set; } = string.Empty;
  public string TimeField { get; set; } = FilterField.CreatedAtKey;

  /// <summary>Вікно, хвилин; 0 - усі записи.</summary>
  public int WindowMinutes { get; set; } = 60;

  public string Operator { get; set; } = "gt";
  public string? Threshold { get; set; }
  public int ConsecutiveChecks { get; set; } = 1;
  public int CheckIntervalSeconds { get; set; } = 60;

  public static readonly IReadOnlyList<(int Minutes, string Label)> Windows =
  [
    (5, "за 5 хв"), (15, "за 15 хв"), (30, "за 30 хв"), (60, "за 1 год"), (120, "за 2 год"), (360, "за 6 год"),
    (720, "за 12 год"), (1440, "за добу"), (10080, "за тиждень"), (43200, "за 30 днів"), (0, "за весь час")
  ];

  public static readonly IReadOnlyList<(int Seconds, string Label)> Intervals =
  [
    (30, "кожні 30 с"), (60, "щохвилини"), (300, "кожні 5 хв"), (900, "кожні 15 хв"), (3600, "щогодини"), (86400, "раз на добу")
  ];

  public static readonly IReadOnlyList<(string Code, string Label)> Operators =
  [
    ("gt", "більше за"), ("gte", "не менше за"), ("lt", "менше за"), ("lte", "не більше за"), ("eq", "дорівнює"), ("neq", "не дорівнює")
  ];

  public static readonly IReadOnlyList<int> ConsecutiveOptions = [1, 2, 3, 5];

  public static RuleForm From(MonitoringRule rule, IReadOnlyList<FilterField> fields) => new()
  {
    ConfigId = rule.ConfigId,
    Name = rule.Name,
    IsEnabled = rule.IsEnabled,
    Filters = rule.Filters.Select(f => f.ToForm(fields)).OfType<RecordFilterForm>().ToList(),
    Search = rule.Search,
    Function = rule.Function,
    Field = rule.Field ?? string.Empty,
    TimeField = rule.TimeField,
    WindowMinutes = rule.WindowMinutes ?? 0,
    Operator = rule.Operator,
    Threshold = (rule.Threshold / 1.0000000000000000000000000000m).ToString(CultureInfo.InvariantCulture),
    ConsecutiveChecks = rule.ConsecutiveChecks,
    CheckIntervalSeconds = rule.CheckIntervalSeconds
  };

  /// <summary>Тіло запиту API (SaveMonitoringRuleCommand).</summary>
  public object ToApi(IReadOnlyList<FilterField> fields) => new
  {
    config = ConfigId?.ToString(),
    name = Name.Trim(),
    isEnabled = IsEnabled,
    filters = Filters
      .Select(f => (Filter: f, Field: fields.FirstOrDefault(x => x.Key == f.Field)))
      .Where(x => x.Field != null)
      .Select(x => x.Filter.ToApi(x.Field!))
      .ToList(),
    search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
    function = Function,
    field = string.IsNullOrEmpty(Field) ? null : Field,
    timeField = TimeField,
    windowMinutes = WindowMinutes > 0 ? WindowMinutes : (int?)null,
    @operator = Operator,
    threshold = FieldValue.ParseNumber(Threshold) ?? 0,
    consecutiveChecks = ConsecutiveChecks,
    checkIntervalSeconds = CheckIntervalSeconds
  };

  /// <summary>Поля, які можна рахувати функцією правила: сума, середнє, мінімум, максимум - лише числа.</summary>
  public static IEnumerable<FilterField> MetricFields(string function, IEnumerable<FilterField> fields) => function == "count"
    ? fields.Where(f => !f.IsSystem)
    : fields.Where(f => !f.IsSystem && f.DataType == PropertyDataType.Number);

  /// <summary>Поля часу для вікна: дата створення / зміни або поле «дата і час».</summary>
  public static IEnumerable<FilterField> TimeFields(IEnumerable<FilterField> fields)
    => fields.Where(f => f.IsSystem || f.DataType == PropertyDataType.DateTime);
}
