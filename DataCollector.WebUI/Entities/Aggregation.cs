using System.Globalization;
using System.Text.Json;
using DataCollector.WebUI.Enums;
using DataCollector.WebUI.Helpers;

namespace DataCollector.WebUI.Entities;

/// <summary>Результат агрегації з API (POST .../records/aggregate).</summary>
public class AggregateResult
{
  public List<AggregateDimensionInfo> Dimensions { get; set; } = [];
  public List<AggregateMetricInfo> Metrics { get; set; } = [];
  public List<AggregateRow> Rows { get; set; } = [];
  public bool Truncated { get; set; }
}

public class AggregateDimensionInfo
{
  public string Field { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public PropertyDataType DataType { get; set; }
  public string? Interval { get; set; }
  public decimal? Step { get; set; }
}

public class AggregateMetricInfo
{
  public string Function { get; set; } = string.Empty;
  public string? Field { get; set; }
  public string Name { get; set; } = string.Empty;
}

public class AggregateRow
{
  public List<JsonElement> Keys { get; set; } = [];
  public List<JsonElement> Values { get; set; } = [];
}

/// <summary>Групування в редакторі зведення.</summary>
public sealed class AggregateDimensionForm
{
  public string Field { get; set; } = string.Empty;
  public string Interval { get; set; } = "day";
  public string? Step { get; set; }

  public object ToApi(FilterField field) => new
  {
    field = field.Key,
    interval = IsDate(field) ? Interval : null,
    step = field.DataType == PropertyDataType.Number && !field.IsSystem &&
           decimal.TryParse(Step?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var step) && step > 0
      ? step
      : (decimal?)null
  };

  public static bool IsDate(FilterField field)
    => field.IsSystem || field.DataType is PropertyDataType.Date or PropertyDataType.DateTime;
}

/// <summary>Показник у редакторі зведення. Порожнє поле для "count" - кількість записів.</summary>
public sealed class AggregateMetricForm
{
  public string Function { get; set; } = "count";
  public string Field { get; set; } = string.Empty;

  public object ToApi() => new
  {
    function = Function,
    field = string.IsNullOrEmpty(Field) ? null : Field
  };
}

/// <summary>Налаштування зведення для моделі: групування, показники і порядок.</summary>
public sealed class SummarySettings
{
  public Guid ConfigId { get; set; }
  public List<AggregateDimensionForm> Dimensions { get; set; } = [];
  public List<AggregateMetricForm> Metrics { get; set; } = [];
  public string Order { get; set; } = "keys";

  /// <summary>Інша модель - починаємо з найпоширенішого зведення: кількість записів по місяцях.</summary>
  public void ResetFor(Guid configId)
  {
    if (ConfigId == configId)
      return;

    ConfigId = configId;
    Dimensions = [new AggregateDimensionForm { Field = FilterField.CreatedAtKey, Interval = "month" }];
    Metrics = [new AggregateMetricForm()];
    Order = "keys";
  }
}

/// <summary>Функції показників і поля, до яких їх можна застосувати.</summary>
public static class AggregateCatalog
{
  public static readonly (string Code, string Label)[] Functions =
  [
    ("count", "Кількість"),
    ("sum", "Сума"),
    ("avg", "Середнє"),
    ("min", "Мінімум"),
    ("max", "Максимум")
  ];

  public static readonly (string Code, string Label)[] Intervals =
  [
    ("hour", "по годинах"),
    ("day", "по днях"),
    ("week", "по тижнях"),
    ("month", "по місяцях"),
    ("quarter", "по кварталах"),
    ("year", "по роках")
  ];

  public static readonly (string Code, string Label)[] Orders =
  [
    ("keys", "за групами"),
    ("valueDesc", "за першим показником ↓"),
    ("valueAsc", "за першим показником ↑")
  ];

  /// <summary>Поля, для яких має сенс функція (count - будь-які, sum/avg - числа, min/max - числа й дати).</summary>
  public static IEnumerable<FilterField> FieldsFor(string function, IEnumerable<FilterField> fields) => function switch
  {
    "sum" or "avg" => fields.Where(f => !f.IsSystem && f.DataType == PropertyDataType.Number),
    "min" or "max" => fields.Where(f => f.IsSystem || f.DataType is PropertyDataType.Number or PropertyDataType.Date or PropertyDataType.DateTime),
    _ => fields
  };

  /// <summary>Інтервали для поля: дата без часу не групується за годинами.</summary>
  public static IEnumerable<(string Code, string Label)> IntervalsFor(FilterField field)
    => field.DataType == PropertyDataType.Date && !field.IsSystem ? Intervals.Skip(1) : Intervals;
}

/// <summary>Підписи значень у таблиці зведення.</summary>
public static class AggregateFormat
{
  private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
  private static readonly string[] Quarters = ["I", "II", "III", "IV"];

  public static string Key(AggregateDimensionInfo dimension, JsonElement key)
  {
    var raw = FieldValue.Raw(key);
    if (raw is null)
      return "(порожньо)";

    if (dimension.Interval is { } interval)
      return IntervalLabel(interval, raw);

    if (dimension.DataType == PropertyDataType.Number && dimension.Step is { } step &&
        decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var from))
    {
      return $"{Number(from)} – {Number(from + step)}";
    }

    return FieldValue.Display(dimension.DataType, raw);
  }

  public static string Value(AggregateMetricInfo metric, PropertyDataType? fieldType, JsonElement value)
  {
    var raw = FieldValue.Raw(value);
    if (raw is null)
      return "—";

    if (metric.Function is "min" or "max" && fieldType is PropertyDataType.Date or PropertyDataType.DateTime)
      return FieldValue.Display(fieldType.Value, raw);

    return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
      ? Number(number)
      : raw;
  }

  public static decimal? Numeric(JsonElement value)
    => value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;

  private static string Number(decimal value) => value.ToString("#,0.####", Ukrainian);

  private static string IntervalLabel(string interval, string raw)
  {
    switch (interval)
    {
      case "year":
        return raw;
      case "quarter" when raw.Length == 7 && int.TryParse(raw[6..], out var quarter) && quarter is >= 1 and <= 4:
        return $"{Quarters[quarter - 1]} кв. {raw[..4]}";
      case "month" when DateTime.TryParseExact(raw, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month):
        return month.ToString("MMMM yyyy", Ukrainian);
      case "hour" when DateTime.TryParseExact(raw, "yyyy-MM-dd'T'HH':00'", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hour):
        return hour.ToString("dd.MM.yyyy HH:00", CultureInfo.InvariantCulture);
      case "week" when DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out var monday):
        return $"тиждень з {monday:dd.MM.yyyy}";
      default:
        return DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out var day)
          ? day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
          : raw;
    }
  }
}
