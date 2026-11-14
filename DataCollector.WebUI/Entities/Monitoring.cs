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
  public string? WindowUnit { get; set; }
  public int WindowOffset { get; set; }
  public bool WindowToDate { get; set; }
  public string? TimeZone { get; set; }
  public string Operator { get; set; } = "gt";
  public decimal Threshold { get; set; }

  /// <summary>Показник, з яким порівнюється значення; null - фіксований поріг <see cref="Threshold"/>.</summary>
  public MonitoringCompare? Compare { get; set; }

  public int ConsecutiveChecks { get; set; }
  public int CheckIntervalSeconds { get; set; }
  public string Condition { get; set; } = string.Empty;
  public string State { get; set; } = RuleStates.Unknown;
  public DateTime? StateSince { get; set; }
  public decimal? LastValue { get; set; }

  /// <summary>Значення показника порівняння на останній перевірці.</summary>
  public decimal? LastCompareValue { get; set; }

  /// <summary>Поріг на останній перевірці (фіксований або показник порівняння × коефіцієнт).</summary>
  public decimal? LastThreshold { get; set; }

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

/// <summary>Показник порівняння правила (з API).</summary>
public class MonitoringCompare
{
  public Guid ConfigId { get; set; }
  public string ConfigName { get; set; } = string.Empty;
  public List<ApiFilter> Filters { get; set; } = [];
  public string? Search { get; set; }
  public string Function { get; set; } = "count";
  public string? Field { get; set; }
  public string TimeField { get; set; } = FilterField.CreatedAtKey;
  public int? WindowMinutes { get; set; }
  public string? WindowUnit { get; set; }
  public int WindowOffset { get; set; }
  public bool WindowToDate { get; set; }
  public decimal Multiplier { get; set; } = 1;
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
  public decimal? CompareValue { get; set; }
  public string? CompareName { get; set; }
  public decimal? Threshold { get; set; }
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

/// <summary>
/// Період показника у формі: ковзне вікно («m:60» - останні 60 хв), календарний період («u:month») або «all» -
/// весь час; плюс зсув назад (0 - поточний, 1 - попередній) і «лише до того ж моменту» для зсунутого календарного.
/// </summary>
public sealed class PeriodForm
{
  public const string AllTime = "all";

  public string Window { get; set; } = "m:60";
  public int Offset { get; set; }
  public bool ToDate { get; set; }

  public bool IsCalendar => Window.StartsWith("u:", StringComparison.Ordinal);
  public bool IsAllTime => Window == AllTime;
  public int? Minutes => Window.StartsWith("m:", StringComparison.Ordinal) && int.TryParse(Window[2..], out var m) ? m : null;
  public string? Unit => IsCalendar ? Window[2..] : null;

  /// <summary>Ковзні вікна.</summary>
  public static readonly IReadOnlyList<(string Code, string Label)> Sliding =
  [
    ("m:5", "5 хв"), ("m:15", "15 хв"), ("m:30", "30 хв"), ("m:60", "1 год"), ("m:120", "2 год"), ("m:360", "6 год"),
    ("m:720", "12 год"), ("m:1440", "доба"), ("m:10080", "тиждень (7 днів)"), ("m:43200", "30 днів"), (AllTime, "весь час")
  ];

  /// <summary>Календарні періоди в часовому поясі користувача.</summary>
  public static readonly IReadOnlyList<(string Code, string Label)> Calendar =
  [
    ("u:hour", "година"), ("u:day", "день (з 00:00)"), ("u:week", "тиждень (з понеділка)"), ("u:month", "місяць (з 1 числа)"),
    ("u:quarter", "квартал"), ("u:year", "рік")
  ];

  public static PeriodForm From(int? minutes, string? unit, int offset, bool toDate) => new()
  {
    Window = unit is { Length: > 0 } ? $"u:{unit}" : minutes is { } m ? $"m:{m}" : AllTime,
    Offset = offset,
    ToDate = toDate
  };

  public PeriodForm Copy() => new() { Window = Window, Offset = Offset, ToDate = ToDate };

  /// <summary>Варіанти зсуву для поточного вікна: поточний, попередній, ... і «той самий торік» для календарних.</summary>
  public IReadOnlyList<(int Offset, string Label)> Shifts()
  {
    if (IsAllTime)
      return [(0, "—")];

    if (!IsCalendar)
      return [(0, "останні"), (1, "попередні"), (2, "ще на період раніше")];

    var feminine = Unit == "hour";
    var list = new List<(int, string)>
    {
      (0, feminine ? "поточна" : "поточний"),
      (1, feminine ? "попередня" : "попередній"),
      (2, "2 періоди тому")
    };

    var yearAgo = Unit switch { "month" => 12, "quarter" => 4, "week" => 52, _ => 0 };
    if (yearAgo > 0)
      list.Add((yearAgo, Unit == "week" ? "той самий тиждень торік" : Unit == "month" ? "той самий місяць торік" : "той самий квартал торік"));
    if (Unit == "day")
      list.Add((7, "той самий день тиждень тому"));
    if (Unit == "hour")
      list.Add((24, "та сама година вчора"));

    // Зсув, заданий через API, якого немає серед варіантів, - не губимо.
    if (list.All(x => x.Item1 != Offset))
      list.Add((Offset, $"{Offset} періодів тому"));

    return list;
  }

  /// <summary>Коротко для підпису умови: «за поточний місяць», «за 1 год», «за попередній місяць до того ж моменту».</summary>
  public string Text()
  {
    if (IsAllTime)
      return "за весь час";

    var shift = Shifts().FirstOrDefault(x => x.Offset == Offset).Label ?? "";

    if (Minutes is { } m)
    {
      var length = m % 1440 == 0 ? $"{m / 1440} дн." : m % 60 == 0 ? $"{m / 60} год" : $"{m} хв";
      return Offset switch
      {
        0 => $"за {length}",
        1 => $"за попередні {length}",
        _ => $"за {length}, {Offset} періоди тому"
      };
    }

    var noun = Unit switch { "hour" => "годину", "day" => "день", "week" => "тиждень", "month" => "місяць", "quarter" => "квартал", _ => "рік" };
    var text = Offset switch
    {
      0 => $"за {(Unit == "hour" ? "поточну" : "поточний")} {noun}",
      1 => $"за {(Unit == "hour" ? "попередню" : "попередній")} {noun}",
      _ when Unit == "hour" && Offset == 24 => "за ту саму годину вчора",
      _ when shift.StartsWith("той самий", StringComparison.Ordinal) => $"за {shift}",
      _ => $"за {noun} {Offset} періоди тому"
    };

    return ToDate && Offset > 0 ? text + " до того ж моменту" : text;
  }
}

/// <summary>Показник порівняння у формі редактора правила.</summary>
public sealed class CompareForm
{
  public Guid? ConfigId { get; set; }
  public string Function { get; set; } = "count";
  public string Field { get; set; } = string.Empty;
  public string TimeField { get; set; } = FilterField.CreatedAtKey;
  public PeriodForm Period { get; set; } = new() { Window = "u:month", Offset = 1, ToDate = true };

  /// <summary>Коефіцієнт як текст поля введення («1,1»).</summary>
  public string? Multiplier { get; set; } = "1";

  /// <summary>Ті самі умови й пошук, що й у показника правила (лише для тієї самої моделі).</summary>
  public bool SameFilters { get; set; } = true;

  public List<RecordFilterForm> Filters { get; set; } = [];
  public string? Search { get; set; }
}

/// <summary>Форма редактора правила.</summary>
public sealed class RuleForm
{
  public const string ThresholdNumber = "number";
  public const string ThresholdMetric = "metric";

  public Guid? ConfigId { get; set; }
  public string Name { get; set; } = string.Empty;
  public bool IsEnabled { get; set; } = true;
  public List<RecordFilterForm> Filters { get; set; } = [];
  public string? Search { get; set; }
  public string Function { get; set; } = "count";
  public string Field { get; set; } = string.Empty;
  public string TimeField { get; set; } = FilterField.CreatedAtKey;
  public PeriodForm Period { get; set; } = new();

  public string Operator { get; set; } = "gt";

  /// <summary>Поріг - число (<see cref="Threshold"/>) чи інший показник (<see cref="Compare"/>).</summary>
  public string ThresholdMode { get; set; } = ThresholdNumber;

  public string? Threshold { get; set; }
  public CompareForm Compare { get; set; } = new();
  public int ConsecutiveChecks { get; set; } = 1;
  public int CheckIntervalSeconds { get; set; } = 60;

  public static readonly IReadOnlyList<(int Seconds, string Label)> Intervals =
  [
    (30, "кожні 30 с"), (60, "щохвилини"), (300, "кожні 5 хв"), (900, "кожні 15 хв"), (3600, "щогодини"), (86400, "раз на добу")
  ];

  public static readonly IReadOnlyList<(string Code, string Label)> Operators =
  [
    ("gt", "більше за"), ("gte", "не менше за"), ("lt", "менше за"), ("lte", "не більше за"), ("eq", "дорівнює"), ("neq", "не дорівнює")
  ];

  public static readonly IReadOnlyList<int> ConsecutiveOptions = [1, 2, 3, 5];

  /// <param name="compareFields">Поля моделі показника порівняння (якщо поріг - показник).</param>
  public static RuleForm From(MonitoringRule rule, IReadOnlyList<FilterField> fields, IReadOnlyList<FilterField>? compareFields = null)
  {
    var form = new RuleForm
    {
      ConfigId = rule.ConfigId,
      Name = rule.Name,
      IsEnabled = rule.IsEnabled,
      Filters = rule.Filters.Select(f => f.ToForm(fields)).OfType<RecordFilterForm>().ToList(),
      Search = rule.Search,
      Function = rule.Function,
      Field = rule.Field ?? string.Empty,
      TimeField = rule.TimeField,
      Period = PeriodForm.From(rule.WindowMinutes, rule.WindowUnit, rule.WindowOffset, rule.WindowToDate),
      Operator = rule.Operator,
      Threshold = (rule.Threshold / 1.0000000000000000000000000000m).ToString(CultureInfo.InvariantCulture),
      ConsecutiveChecks = rule.ConsecutiveChecks,
      CheckIntervalSeconds = rule.CheckIntervalSeconds
    };

    if (rule.Compare is { } c)
    {
      var sameFilters = c.ConfigId == rule.ConfigId
        && string.Equals(c.Search ?? "", rule.Search ?? "", StringComparison.Ordinal)
        && JsonSerializer.Serialize(c.Filters) == JsonSerializer.Serialize(rule.Filters);

      form.ThresholdMode = ThresholdMetric;
      form.Compare = new CompareForm
      {
        ConfigId = c.ConfigId,
        Function = c.Function,
        Field = c.Field ?? string.Empty,
        TimeField = c.TimeField,
        Period = PeriodForm.From(c.WindowMinutes, c.WindowUnit, c.WindowOffset, c.WindowToDate),
        Multiplier = (c.Multiplier / 1.0000000000000000000000000000m).ToString(CultureInfo.InvariantCulture),
        SameFilters = sameFilters,
        Filters = sameFilters ? [] : c.Filters.Select(f => f.ToForm(compareFields ?? [])).OfType<RecordFilterForm>().ToList(),
        Search = sameFilters ? null : c.Search
      };
    }

    return form;
  }

  /// <summary>Тіло запиту API (SaveMonitoringRuleCommand).</summary>
  /// <param name="compareFields">Поля моделі показника порівняння.</param>
  public object ToApi(IReadOnlyList<FilterField> fields, IReadOnlyList<FilterField>? compareFields = null)
  {
    var compare = ThresholdMode == ThresholdMetric ? Compare : null;
    var sameFilters = compare is { SameFilters: true } && compare.ConfigId == ConfigId;

    return new
    {
      config = ConfigId?.ToString(),
      name = Name.Trim(),
      isEnabled = IsEnabled,
      filters = ApiFilters(Filters, fields),
      search = Trimmed(Search),
      function = Function,
      field = string.IsNullOrEmpty(Field) ? null : Field,
      timeField = TimeField,
      windowMinutes = Period.Minutes,
      windowUnit = Period.Unit,
      windowOffset = Period.IsAllTime ? 0 : Period.Offset,
      windowToDate = Period.ToDate,
      // Межі дня, тижня, місяця - за часовим поясом, у якому працює інтерфейс.
      timeZone = TimeZoneInfo.Local.Id,
      @operator = Operator,
      threshold = compare is null ? FieldValue.ParseNumber(Threshold) ?? 0 : 0,
      compare = compare is null ? null : new
      {
        config = compare.ConfigId?.ToString(),
        filters = sameFilters ? ApiFilters(Filters, fields) : ApiFilters(compare.Filters, compareFields ?? []),
        search = sameFilters ? Trimmed(Search) : Trimmed(compare.Search),
        function = compare.Function,
        field = string.IsNullOrEmpty(compare.Field) ? null : compare.Field,
        timeField = compare.TimeField,
        windowMinutes = compare.Period.Minutes,
        windowUnit = compare.Period.Unit,
        windowOffset = compare.Period.IsAllTime ? 0 : compare.Period.Offset,
        windowToDate = compare.Period.ToDate,
        multiplier = FieldValue.ParseNumber(compare.Multiplier) ?? 1
      },
      consecutiveChecks = ConsecutiveChecks,
      checkIntervalSeconds = CheckIntervalSeconds
    };
  }

  private static List<object> ApiFilters(IEnumerable<RecordFilterForm> filters, IReadOnlyList<FilterField> fields) => filters
    .Select(f => (Filter: f, Field: fields.FirstOrDefault(x => x.Key == f.Field)))
    .Where(x => x.Field != null)
    .Select(x => x.Filter.ToApi(x.Field!))
    .ToList();

  private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

  /// <summary>Поля, які можна рахувати функцією правила: сума, середнє, мінімум, максимум - лише числа.</summary>
  public static IEnumerable<FilterField> MetricFields(string function, IEnumerable<FilterField> fields) => function == "count"
    ? fields.Where(f => !f.IsSystem)
    : fields.Where(f => !f.IsSystem && f.DataType == PropertyDataType.Number);

  /// <summary>Поля часу для вікна: дата створення / зміни або поле «дата і час».</summary>
  public static IEnumerable<FilterField> TimeFields(IEnumerable<FilterField> fields)
    => fields.Where(f => f.IsSystem || f.DataType == PropertyDataType.DateTime);
}
