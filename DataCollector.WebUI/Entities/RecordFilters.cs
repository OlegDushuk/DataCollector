using DataCollector.WebUI.Enums;
using DataCollector.WebUI.Helpers;

namespace DataCollector.WebUI.Entities;

/// <summary>Скільки значень потребує оператор фільтра.</summary>
public enum FilterArity
{
  None,
  Single,
  Range,
  Multiple
}

/// <summary>Оператор фільтра: код в API, підпис в інтерфейсі і кількість значень.</summary>
public sealed record FilterOperatorInfo(string Code, string Label, FilterArity Arity);

/// <summary>
/// Поле, за яким можна фільтрувати: поле моделі або системне (дата створення / зміни запису).
/// </summary>
public sealed class FilterField
{
  public const string CreatedAtKey = "createdAt";
  public const string UpdatedAtKey = "updatedAt";

  public string Key { get; init; } = string.Empty;
  public string Name { get; init; } = string.Empty;
  public PropertyDataType DataType { get; init; }
  public IReadOnlyList<string> Options { get; init; } = [];
  public bool IsSystem { get; init; }

  /// <summary>Доступні оператори; перший - оператор за замовчуванням для нової умови.</summary>
  public IReadOnlyList<FilterOperatorInfo> Operators => FilterCatalog.For(this);

  public FilterOperatorInfo? FindOperator(string? code)
    => Operators.FirstOrDefault(o => o.Code == code);

  /// <summary>Поля моделі + системні дати, у порядку відображення в таблиці.</summary>
  public static List<FilterField> ForConfig(EntityConfig config)
  {
    var fields = config.Properties
      .Select(p => new FilterField
      {
        Key = p.Key,
        Name = p.Name,
        DataType = p.DataType,
        Options = p.Rules.Options ?? []
      })
      .ToList();

    fields.Add(new FilterField { Key = CreatedAtKey, Name = "Створено", DataType = PropertyDataType.DateTime, IsSystem = true });
    fields.Add(new FilterField { Key = UpdatedAtKey, Name = "Змінено", DataType = PropertyDataType.DateTime, IsSystem = true });

    return fields;
  }
}

/// <summary>
/// Умова фільтра в редакторі. Значення - у форматі полів форми (дата й час - місцевий час).
/// </summary>
public sealed class RecordFilterForm
{
  public string Field { get; set; } = string.Empty;
  public string Operator { get; set; } = string.Empty;
  public string? Value { get; set; }
  public string? ValueTo { get; set; }
  public List<string> Values { get; set; } = [];

  public RecordFilterForm Clone() => new()
  {
    Field = Field,
    Operator = Operator,
    Value = Value,
    ValueTo = ValueTo,
    Values = [.. Values]
  };

  /// <summary>Нова умова для поля з оператором за замовчуванням.</summary>
  public static RecordFilterForm For(FilterField field) => new()
  {
    Field = field.Key,
    Operator = field.Operators[0].Code
  };

  /// <summary>Чи заповнені всі значення, яких потребує оператор.</summary>
  public bool IsComplete(FilterField field)
  {
    return field.FindOperator(Operator)?.Arity switch
    {
      FilterArity.None => true,
      FilterArity.Single => !string.IsNullOrWhiteSpace(Value),
      FilterArity.Range => !string.IsNullOrWhiteSpace(Value) && !string.IsNullOrWhiteSpace(ValueTo),
      FilterArity.Multiple => Values.Count > 0,
      _ => false
    };
  }

  /// <summary>Умова для API (RecordFilterDto).</summary>
  public object ToApi(FilterField field)
  {
    var arity = field.FindOperator(Operator)?.Arity ?? FilterArity.None;

    return new
    {
      field = field.Key,
      @operator = Operator,
      value = arity is FilterArity.Single or FilterArity.Range ? FieldValue.ToApi(field.DataType, Value) : null,
      valueTo = arity == FilterArity.Range ? FieldValue.ToApi(field.DataType, ValueTo) : null,
      values = arity == FilterArity.Multiple ? Values : null
    };
  }

  /// <summary>Короткий опис для «чіпа» над таблицею: "Ціна: між 10 і 100".</summary>
  public string Describe(FilterField field)
  {
    var op = field.FindOperator(Operator);
    if (op is null)
      return field.Name;

    string Show(string? value) => FieldValue.Display(field.DataType, FieldValue.ToApi(field.DataType, value));

    var values = op.Arity switch
    {
      FilterArity.Single => Show(Value),
      FilterArity.Range => $"{Show(Value)} і {Show(ValueTo)}",
      FilterArity.Multiple => Values.Count <= 3 ? string.Join(", ", Values) : $"{Values.Count} значень",
      _ => string.Empty
    };

    return $"{field.Name}: {op.Label} {values}".Trim();
  }
}

/// <summary>
/// Які оператори пропонує інтерфейс для кожного типу поля. Сервер приймає ширший набір (див. README),
/// тут - ті, що зручні в формі.
/// </summary>
public static class FilterCatalog
{
  private static readonly FilterOperatorInfo IsEmpty = new("isEmpty", "порожнє", FilterArity.None);
  private static readonly FilterOperatorInfo IsNotEmpty = new("isNotEmpty", "заповнене", FilterArity.None);

  private static readonly FilterOperatorInfo[] TextOps =
  [
    new("contains", "містить", FilterArity.Single),
    new("notContains", "не містить", FilterArity.Single),
    new("eq", "дорівнює", FilterArity.Single),
    new("neq", "не дорівнює", FilterArity.Single),
    new("startsWith", "починається з", FilterArity.Single),
    new("endsWith", "закінчується на", FilterArity.Single),
    IsEmpty,
    IsNotEmpty
  ];

  private static readonly FilterOperatorInfo[] NumberOps =
  [
    new("between", "між", FilterArity.Range),
    new("eq", "=", FilterArity.Single),
    new("neq", "≠", FilterArity.Single),
    new("gt", ">", FilterArity.Single),
    new("gte", "≥", FilterArity.Single),
    new("lt", "<", FilterArity.Single),
    new("lte", "≤", FilterArity.Single),
    IsEmpty,
    IsNotEmpty
  ];

  private static readonly FilterOperatorInfo[] DateOps =
  [
    new("between", "між", FilterArity.Range),
    new("eq", "дорівнює", FilterArity.Single),
    new("gte", "з", FilterArity.Single),
    new("lte", "по", FilterArity.Single),
    IsEmpty,
    IsNotEmpty
  ];

  private static readonly FilterOperatorInfo[] DateTimeOps =
  [
    new("between", "між", FilterArity.Range),
    new("gte", "з", FilterArity.Single),
    new("lte", "по", FilterArity.Single),
    IsEmpty,
    IsNotEmpty
  ];

  private static readonly FilterOperatorInfo[] BooleanOps =
  [
    new("eq", "дорівнює", FilterArity.Single),
    IsEmpty,
    IsNotEmpty
  ];

  private static readonly FilterOperatorInfo[] EnumOps =
  [
    new("in", "одне з", FilterArity.Multiple),
    new("notIn", "жодне з", FilterArity.Multiple),
    IsEmpty,
    IsNotEmpty
  ];

  public static IReadOnlyList<FilterOperatorInfo> For(FilterField field)
  {
    if (field.IsSystem)
    {
      // Дата створення є завжди; дата зміни - лише у змінених записів.
      return field.Key == FilterField.CreatedAtKey
        ? DateTimeOps[..3]
        : [.. DateTimeOps[..3], new("isEmpty", "ніколи", FilterArity.None), new("isNotEmpty", "хоч раз", FilterArity.None)];
    }

    return field.DataType switch
    {
      PropertyDataType.Number => NumberOps,
      PropertyDataType.Boolean => BooleanOps,
      PropertyDataType.Date => DateOps,
      PropertyDataType.DateTime => DateTimeOps,
      PropertyDataType.Enum => EnumOps,
      _ => TextOps
    };
  }
}
