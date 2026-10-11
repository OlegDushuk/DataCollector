using DataCollector.Server.Domain.Common;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Querying;

/// <summary>
/// Умова фільтра "поле - оператор - значення" з уже перевіреними й типізованими значеннями.
/// </summary>
/// <remarks>
/// Заперечні оператори (NotEquals, NotContains, NotIn) включають записи з порожнім значенням поля:
/// "статус не дорівнює Скасоване" показує і записи без статусу.
/// </remarks>
public sealed class RecordFilter
{
  public const int MaxInValues = 100;

  private static readonly FilterOperator[] EmptinessOperators = [FilterOperator.IsEmpty, FilterOperator.IsNotEmpty];

  private static readonly FilterOperator[] ComparisonOperators =
  [
    FilterOperator.Equals, FilterOperator.NotEquals,
    FilterOperator.GreaterThan, FilterOperator.GreaterOrEqual,
    FilterOperator.LessThan, FilterOperator.LessOrEqual,
    FilterOperator.Between
  ];

  private static readonly FilterOperator[] TextOperators =
  [
    FilterOperator.Equals, FilterOperator.NotEquals,
    FilterOperator.Contains, FilterOperator.NotContains,
    FilterOperator.StartsWith, FilterOperator.EndsWith,
    FilterOperator.In, FilterOperator.NotIn
  ];

  private static readonly FilterOperator[] EnumOperators =
    [FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn];

  private static readonly FilterOperator[] BooleanOperators = [FilterOperator.Equals, FilterOperator.NotEquals];

  public RecordField Field { get; }
  public FilterOperator Operator { get; }

  /// <summary>
  /// Типізовані значення: жодного для IsEmpty/IsNotEmpty, два для Between (від, до), кілька для In/NotIn, інакше одне.
  /// </summary>
  public IReadOnlyList<object> Values { get; }

  private RecordFilter(RecordField field, FilterOperator op, IReadOnlyList<object> values)
  {
    Field = field;
    Operator = op;
    Values = values;
  }

  /// <summary>
  /// Створює умову з текстових значень (як вони прийшли з API) і перевіряє, що оператор підходить для типу поля.
  /// </summary>
  /// <param name="value">Значення для операторів з одним значенням; "від" для Between.</param>
  /// <param name="valueTo">"До" для Between.</param>
  /// <param name="values">Список для In / NotIn.</param>
  public static RecordFilter Create(
    RecordField field,
    FilterOperator op,
    string? value,
    string? valueTo = null,
    IReadOnlyList<string?>? values = null)
  {
    if (!AllowedOperators(field).Contains(op))
      throw new DomainValidationException($"Фільтр за полем '{field.Name}': умова «{DisplayName(op)}» не підходить для цього типу поля");

    var parsed = op switch
    {
      FilterOperator.IsEmpty or FilterOperator.IsNotEmpty => [],
      FilterOperator.Between => ParseRange(field, value, valueTo),
      FilterOperator.In or FilterOperator.NotIn => ParseList(field, values),
      _ => [ParseRequired(field, value)]
    };

    return new RecordFilter(field, op, parsed);
  }

  /// <summary>Оператори, доступні для поля. Системні поля (дати запису) - лише порівняння.</summary>
  public static IReadOnlyList<FilterOperator> AllowedOperators(RecordField field)
  {
    if (field.SystemField == RecordSystemField.CreatedAt)
      return ComparisonOperators;

    if (field.SystemField == RecordSystemField.UpdatedAt)
      return [.. ComparisonOperators, .. EmptinessOperators];

    FilterOperator[] typeOperators = field.Type switch
    {
      PropertyDataType.Text => TextOperators,
      PropertyDataType.Enum => EnumOperators,
      PropertyDataType.Boolean => BooleanOperators,
      _ => ComparisonOperators
    };

    return [.. typeOperators, .. EmptinessOperators];
  }

  /// <summary>Назва оператора для повідомлень.</summary>
  public static string DisplayName(FilterOperator op) => op switch
  {
    FilterOperator.Equals => "дорівнює",
    FilterOperator.NotEquals => "не дорівнює",
    FilterOperator.GreaterThan => "більше",
    FilterOperator.GreaterOrEqual => "не менше",
    FilterOperator.LessThan => "менше",
    FilterOperator.LessOrEqual => "не більше",
    FilterOperator.Between => "між",
    FilterOperator.Contains => "містить",
    FilterOperator.NotContains => "не містить",
    FilterOperator.StartsWith => "починається з",
    FilterOperator.EndsWith => "закінчується на",
    FilterOperator.In => "одне з",
    FilterOperator.NotIn => "жодне з",
    FilterOperator.IsEmpty => "порожнє",
    FilterOperator.IsNotEmpty => "не порожнє",
    _ => op.ToString()
  };

  private static object ParseRequired(RecordField field, string? value)
  {
    return PropertyValueParser.Parse(field.Type, field.Name, value)
           ?? throw new DomainValidationException($"Фільтр за полем '{field.Name}': не вказано значення");
  }

  private static object[] ParseRange(RecordField field, string? from, string? to)
  {
    var min = ParseRequired(field, from);
    var max = ParseRequired(field, to);

    if (Comparer<object>.Default.Compare(min, max) > 0)
      throw new DomainValidationException($"Фільтр за полем '{field.Name}': початок діапазону більший за кінець");

    return [min, max];
  }

  private static object[] ParseList(RecordField field, IReadOnlyList<string?>? values)
  {
    var parsed = (values ?? [])
      .Select(v => PropertyValueParser.Parse(field.Type, field.Name, v))
      .OfType<object>()
      .Distinct()
      .ToArray();

    if (parsed.Length == 0)
      throw new DomainValidationException($"Фільтр за полем '{field.Name}': потрібно вибрати хоча б одне значення");

    if (parsed.Length > MaxInValues)
      throw new DomainValidationException($"Фільтр за полем '{field.Name}': не більше {MaxInValues} значень");

    return parsed;
  }
}
