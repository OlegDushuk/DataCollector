using System.Data;
using System.Text;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.DataAccess.Querying;

/// <summary>Параметр SQL-команди: ім'я (без @), значення і тип.</summary>
internal sealed record SqlParameterValue(string Name, object? Value, DbType DbType);

/// <summary>
/// Спільна основа динамічних запитів до записів (<see cref="RecordQuerySql"/>, <see cref="RecordAggregateSql"/>):
/// параметри, змінні з ключами полів, умови фільтра і пошуку.
/// </summary>
/// <remarks>
/// <para>
/// У текст SQL потрапляють лише фіксовані фрагменти, імена колонок і оператори з білого списку
/// (залежать від типу поля та enum-оператора) та згенеровані імена параметрів.
/// Усі значення користувача, включно з ідентифікаторами полів, - тільки параметрами. SQL-ін'єкція неможлива.
/// </para>
/// <para>
/// Кожна умова за полем моделі - це EXISTS по EntityPropertyInstances для пари (запис, поле) з порівнянням
/// у колонці типу поля. Умова "IS NOT NULL" дозволяє оптимізатору брати фільтровані індекси
/// (PropertyPkey, ValueNumber / ValueBool / ValueDate). OPTION (RECOMPILE) будує план під конкретні значення.
/// </para>
/// </remarks>
internal abstract class RecordSqlBuilder
{
  protected const string ValuesTable = "[dbo].[EntityPropertyInstances]";

  private readonly StringBuilder _declarations = new();
  private readonly List<SqlParameterValue> _parameters = [];
  private readonly Dictionary<Guid, string> _propertyVariables = [];
  private int _counter;

  public string Sql { get; protected set; } = string.Empty;
  public IReadOnlyList<SqlParameterValue> Parameters => _parameters;

  /// <summary>Оголошення змінних: ключ моделі і ключі полів, що використовуються в запиті.</summary>
  protected string Declarations =>
    "DECLARE @ConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @ConfigId);\n" +
    _declarations;

  /// <summary>WHERE для записів моделі: умови фільтра (через І) і текстовий пошук.</summary>
  protected string WhereClause(EntityConfig config, IReadOnlyList<RecordFilter> filters, string? search)
  {
    AddParameter("ConfigId", config.Id, DbType.Guid);

    var conditions = new List<string> { "i.[ConfigPkey] = @ConfigPkey" };
    conditions.AddRange(filters.Select(Condition));

    if (search != null)
      conditions.Add(SearchCondition(config, search));

    return string.Join("\n  AND ", conditions);
  }

  private string Condition(RecordFilter filter)
  {
    return filter.Field.Property is { } property
      ? PropertyCondition(property, filter)
      : SystemFieldCondition(filter);
  }

  /// <summary>
  /// Умова за полем моделі. Позитивні оператори - "існує значення, що задовольняє порівняння";
  /// заперечні - "не існує значення, що задовольняє протилежне" (тож порожні значення теж підходять).
  /// </summary>
  private string PropertyCondition(EntityPropertyConfig property, RecordFilter filter)
  {
    var pkey = PropertyVariable(property);
    var column = $"v.{ValueColumn(property.Type)}";

    string Exists(string? predicate) =>
      $"EXISTS (SELECT 1 FROM {ValuesTable} v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = {pkey}" +
      (predicate is null ? ")" : $" AND {column} IS NOT NULL AND {predicate})");

    return filter.Operator switch
    {
      FilterOperator.IsEmpty => $"NOT {Exists(null)}",
      FilterOperator.IsNotEmpty => Exists(null),

      FilterOperator.NotEquals => $"NOT {Exists(Predicate(column, FilterOperator.Equals, filter))}",
      FilterOperator.NotContains => $"NOT {Exists(Predicate(column, FilterOperator.Contains, filter))}",
      FilterOperator.NotIn => $"NOT {Exists(Predicate(column, FilterOperator.In, filter))}",

      _ => Exists(Predicate(column, filter.Operator, filter))
    };
  }

  private string SystemFieldCondition(RecordFilter filter)
  {
    var column = SystemColumn(filter.Field);

    return filter.Operator switch
    {
      FilterOperator.IsEmpty => $"{column} IS NULL",
      FilterOperator.IsNotEmpty => $"{column} IS NOT NULL",
      FilterOperator.NotEquals => $"({column} IS NULL OR NOT ({Predicate(column, FilterOperator.Equals, filter)}))",
      _ => Predicate(column, filter.Operator, filter)
    };
  }

  /// <summary>Порівняння значення колонки з параметрами для "позитивного" оператора.</summary>
  private string Predicate(string column, FilterOperator op, RecordFilter filter)
  {
    var values = filter.Values;

    return op switch
    {
      FilterOperator.Equals => $"{column} = {Value(values[0])}",
      FilterOperator.GreaterThan => $"{column} > {Value(values[0])}",
      FilterOperator.GreaterOrEqual => $"{column} >= {Value(values[0])}",
      FilterOperator.LessThan => $"{column} < {Value(values[0])}",
      FilterOperator.LessOrEqual => $"{column} <= {Value(values[0])}",
      FilterOperator.Between => $"{column} >= {Value(values[0])} AND {column} <= {Value(values[1])}",

      // LIKE з екрануванням спецсимволів: пошук "50%" шукає саме "50%", а не "50 і будь-що".
      FilterOperator.Contains => Like(column, $"%{EscapeLike((string)values[0])}%"),
      FilterOperator.StartsWith => Like(column, $"{EscapeLike((string)values[0])}%"),
      FilterOperator.EndsWith => Like(column, $"%{EscapeLike((string)values[0])}"),

      FilterOperator.In => $"{column} IN ({string.Join(", ", values.Select(Value))})",

      _ => throw new InvalidOperationException($"Оператор {op} не має прямого порівняння")
    };
  }

  /// <summary>
  /// Пошук підрядка в усіх текстових полях і полях-переліках: достатньо збігу в одному з них.
  /// </summary>
  private string SearchCondition(EntityConfig config, string search)
  {
    var properties = config.Properties.Where(p => RecordQuery.IsSearchable(p.Type)).ToList();

    if (properties.Count == 0)
      return "1 = 0";

    var pkeys = string.Join(", ", properties.Select(PropertyVariable));
    var pattern = AddParameter(NextName("Search"), $"%{EscapeLike(search)}%", DbType.String);

    return $"EXISTS (SELECT 1 FROM {ValuesTable} v WHERE v.[InstancePkey] = i.[Pkey] " +
           $"AND v.[PropertyPkey] IN ({pkeys}) AND v.[ValueText] IS NOT NULL AND v.[ValueText] LIKE {pattern} ESCAPE N'\\')";
  }

  /// <summary>
  /// Вираз для сортування переліку в порядку варіантів, заданому в правилах поля, а не за алфавітом.
  /// Значення, яких немає серед варіантів, дають NULL і йдуть разом з порожніми.
  /// </summary>
  protected string EnumOrder(EntityPropertyConfig property, string valueExpression)
  {
    return "CASE " + valueExpression + " " +
           string.Join(" ", property.Options.Select((o, index) => $"WHEN {Value(o)} THEN {index}")) +
           " END";
  }

  /// <summary>Змінна з внутрішнім ключем поля (один раз на поле).</summary>
  protected string PropertyVariable(EntityPropertyConfig property)
  {
    if (_propertyVariables.TryGetValue(property.Id, out var variable))
      return variable;

    var name = NextName("P");
    var idParameter = AddParameter(name + "Id", property.Id, DbType.Guid);
    variable = "@" + name;

    _declarations.AppendLine(
      $"DECLARE {variable} INT = (SELECT [Pkey] FROM [dbo].[EntityPropertyConfigs] WHERE [Id] = {idParameter});");

    _propertyVariables[property.Id] = variable;
    return variable;
  }

  private string Like(string column, string pattern)
    => $"{column} LIKE {AddParameter(NextName("F"), pattern, DbType.String)} ESCAPE N'\\'";

  /// <summary>Параметр зі значенням відповідного SQL-типу.</summary>
  protected string Value(object value)
  {
    var name = NextName("F");

    return value switch
    {
      decimal number => AddParameter(name, number, DbType.Decimal),
      bool flag => AddParameter(name, flag, DbType.Boolean),
      DateOnly date => AddParameter(name, date.ToDateTime(TimeOnly.MinValue), DbType.DateTime2),
      DateTime dateTime => AddParameter(name, dateTime, DbType.DateTime2),
      int number => AddParameter(name, number, DbType.Int32),
      _ => AddParameter(name, Convert.ToString(value), DbType.String)
    };
  }

  protected string AddParameter(string name, object? value, DbType dbType)
  {
    _parameters.Add(new SqlParameterValue(name, value, dbType));
    return "@" + name;
  }

  protected string NextName(string prefix) => $"{prefix}{_counter++}";

  protected static string ValueColumn(PropertyDataType type) => type switch
  {
    PropertyDataType.Number => "[ValueNumber]",
    PropertyDataType.Boolean => "[ValueBool]",
    PropertyDataType.Date or PropertyDataType.DateTime => "[ValueDate]",
    _ => "[ValueText]"
  };

  protected static string SystemColumn(RecordField field)
    => field.SystemField == RecordSystemField.UpdatedAt ? "i.[UpdatedAt]" : "i.[CreatedAt]";

  /// <summary>Екранування символів шаблону LIKE (з ESCAPE '\').</summary>
  private static string EscapeLike(string value) => value
    .Replace(@"\", @"\\")
    .Replace("%", @"\%")
    .Replace("_", @"\_")
    .Replace("[", @"\[");
}
