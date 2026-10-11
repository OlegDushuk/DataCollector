using System.Data;
using System.Text;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.DataAccess.Querying;

/// <summary>Параметр SQL-команди: ім'я (без @), значення і тип.</summary>
internal sealed record SqlParameterValue(string Name, object? Value, DbType DbType);

/// <summary>
/// Будує параметризований T-SQL для <see cref="RecordQuery"/>: кількість записів, сторінку і значення полів цих записів.
/// </summary>
/// <remarks>
/// <para>
/// Набір умов фільтра довільний, тому запит збирається динамічно, а не фіксованою збереженою процедурою.
/// У текст SQL потрапляють лише імена колонок і оператори з білого списку (залежать від типу поля),
/// а всі значення користувача, включно з ідентифікаторами полів, - тільки параметрами. SQL-ін'єкція неможлива.
/// </para>
/// <para>
/// Кожна умова за полем моделі - це EXISTS по EntityPropertyInstances для пари (запис, поле) з порівнянням
/// у колонці типу поля. Умова "IS NOT NULL" дозволяє оптимізатору брати фільтровані індекси
/// (PropertyPkey, ValueNumber / ValueBool / ValueDate). OPTION (RECOMPILE) будує план під конкретні значення:
/// вибірковість умов сильно різниться ("ціна > 0" і "ціна > 19 000").
/// </para>
/// </remarks>
internal sealed class RecordQuerySql
{
  private const string ValuesTable = "[dbo].[EntityPropertyInstances]";

  private readonly StringBuilder _declarations = new();
  private readonly List<SqlParameterValue> _parameters = [];
  private readonly Dictionary<Guid, string> _propertyVariables = [];
  private int _counter;

  public string Sql { get; private set; } = string.Empty;
  public IReadOnlyList<SqlParameterValue> Parameters => _parameters;

  private RecordQuerySql()
  {
  }

  public static RecordQuerySql Build(EntityConfig config, RecordQuery query)
  {
    var builder = new RecordQuerySql();
    builder.Sql = builder.BuildSql(config, query);
    return builder;
  }

  private string BuildSql(EntityConfig config, RecordQuery query)
  {
    AddParameter("ConfigId", config.Id, DbType.Guid);
    AddParameter("Offset", (query.PageNumber - 1) * query.PageSize, DbType.Int32);
    AddParameter("PageSize", query.PageSize, DbType.Int32);

    var conditions = new List<string> { "i.[ConfigPkey] = @ConfigPkey" };
    conditions.AddRange(query.Filters.Select(Condition));

    if (query.Search != null)
      conditions.Add(SearchCondition(config, query.Search));

    var where = string.Join("\n  AND ", conditions);
    var (sortJoin, orderBy) = Sorting(query);

    var sql = new StringBuilder();
    sql.AppendLine("SET NOCOUNT ON;");
    sql.AppendLine();
    sql.AppendLine("-- Внутрішні ключі моделі та полів: далі всі зв'язки через них.");
    sql.AppendLine("DECLARE @ConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @ConfigId);");
    sql.Append(_declarations);
    sql.AppendLine();
    sql.AppendLine("SELECT COUNT(*) AS [TotalCount]");
    sql.AppendLine("FROM [dbo].[EntityInstances] i");
    sql.AppendLine($"WHERE {where}");
    sql.AppendLine("OPTION (RECOMPILE);");
    sql.AppendLine();
    sql.AppendLine("""
      DECLARE @Page TABLE (
          [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
          [Pkey] BIGINT NOT NULL,
          [Id] UNIQUEIDENTIFIER NOT NULL,
          [CreatedAt] DATETIME2 NOT NULL,
          [UpdatedAt] DATETIME2 NULL);

      INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt])
      SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt]
      FROM [dbo].[EntityInstances] i
      """);
    if (sortJoin != null)
      sql.AppendLine(sortJoin);
    sql.AppendLine($"WHERE {where}");
    sql.AppendLine($"ORDER BY {orderBy}");
    sql.AppendLine("OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
    sql.AppendLine("OPTION (RECOMPILE);");
    sql.AppendLine();
    sql.AppendLine("""
      SELECT [Id], @ConfigId AS [EntityConfigId], [CreatedAt], [UpdatedAt]
      FROM @Page
      ORDER BY [RowNum];

      SELECT
          pg.[Id] AS [EntityInstanceId],
          p.[Id] AS [EntityPropertyConfigId],
          v.[ValueText], v.[ValueNumber], v.[ValueBool], v.[ValueDate]
      FROM @Page pg
      INNER JOIN [dbo].[EntityPropertyInstances] v ON v.[InstancePkey] = pg.[Pkey]
      INNER JOIN [dbo].[EntityPropertyConfigs] p ON p.[Pkey] = v.[PropertyPkey];
      """);

    return sql.ToString();
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
    var column = filter.Field.SystemField == RecordSystemField.UpdatedAt ? "i.[UpdatedAt]" : "i.[CreatedAt]";

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
    var type = filter.Field.Type;
    var values = filter.Values;

    return op switch
    {
      FilterOperator.Equals => $"{column} = {Value(type, values[0])}",
      FilterOperator.GreaterThan => $"{column} > {Value(type, values[0])}",
      FilterOperator.GreaterOrEqual => $"{column} >= {Value(type, values[0])}",
      FilterOperator.LessThan => $"{column} < {Value(type, values[0])}",
      FilterOperator.LessOrEqual => $"{column} <= {Value(type, values[0])}",
      FilterOperator.Between => $"{column} >= {Value(type, values[0])} AND {column} <= {Value(type, values[1])}",

      // LIKE з екрануванням спецсимволів: пошук "50%" шукає саме "50%", а не "50 і будь-що".
      FilterOperator.Contains => Like(column, $"%{EscapeLike((string)values[0])}%"),
      FilterOperator.StartsWith => Like(column, $"{EscapeLike((string)values[0])}%"),
      FilterOperator.EndsWith => Like(column, $"%{EscapeLike((string)values[0])}"),

      FilterOperator.In => $"{column} IN ({string.Join(", ", values.Select(v => Value(type, v)))})",

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
  /// Сортування: за полем моделі - через LEFT JOIN його значення (записи без значення йдуть як NULL),
  /// за системним полем - напряму. Для однакових значень порядок задає дата створення і ключ запису.
  /// </summary>
  private (string? Join, string OrderBy) Sorting(RecordQuery query)
  {
    var direction = query.SortDescending ? "DESC" : "ASC";
    var tieBreak = $"i.[CreatedAt] {direction}, i.[Pkey] {direction}";

    if (query.SortField.Property is not { } property)
    {
      var column = query.SortField.SystemField == RecordSystemField.UpdatedAt ? "i.[UpdatedAt]" : "i.[CreatedAt]";
      return (null, $"{column} {direction}, i.[Pkey] {direction}");
    }

    var join = $"LEFT JOIN {ValuesTable} s ON s.[InstancePkey] = i.[Pkey] AND s.[PropertyPkey] = {PropertyVariable(property)}";

    // Перелік сортується в порядку варіантів, заданому в правилах поля, а не за алфавітом.
    var sortExpression = property.Type == PropertyDataType.Enum && property.Options.Count > 0
      ? "CASE s.[ValueText] " +
        string.Join(" ", property.Options.Select((o, index) => $"WHEN {Value(PropertyDataType.Text, o)} THEN {index}")) +
        " END"
      : $"s.{ValueColumn(property.Type)}";

    return (join, $"{sortExpression} {direction}, {tieBreak}");
  }

  /// <summary>Змінна з внутрішнім ключем поля (один раз на поле).</summary>
  private string PropertyVariable(EntityPropertyConfig property)
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

  /// <summary>Параметр зі значенням фільтра відповідного SQL-типу.</summary>
  private string Value(PropertyDataType type, object value)
  {
    var name = NextName("F");

    return value switch
    {
      decimal number => AddParameter(name, number, DbType.Decimal),
      bool flag => AddParameter(name, flag, DbType.Boolean),
      DateOnly date => AddParameter(name, date.ToDateTime(TimeOnly.MinValue), DbType.DateTime2),
      DateTime dateTime => AddParameter(name, dateTime, DbType.DateTime2),
      _ => AddParameter(name, Convert.ToString(value), DbType.String)
    };
  }

  private string AddParameter(string name, object? value, DbType dbType)
  {
    _parameters.Add(new SqlParameterValue(name, value, dbType));
    return "@" + name;
  }

  private string NextName(string prefix) => $"{prefix}{_counter++}";

  private static string ValueColumn(PropertyDataType type) => type switch
  {
    PropertyDataType.Number => "[ValueNumber]",
    PropertyDataType.Boolean => "[ValueBool]",
    PropertyDataType.Date or PropertyDataType.DateTime => "[ValueDate]",
    _ => "[ValueText]"
  };

  /// <summary>Екранування символів шаблону LIKE (з ESCAPE '\').</summary>
  private static string EscapeLike(string value) => value
    .Replace(@"\", @"\\")
    .Replace("%", @"\%")
    .Replace("_", @"\_")
    .Replace("[", @"\[");
}
