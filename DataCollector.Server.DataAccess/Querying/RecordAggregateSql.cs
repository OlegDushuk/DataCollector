using System.Data;
using System.Text;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.DataAccess.Querying;

/// <summary>
/// Параметризований T-SQL для <see cref="AggregateQuery"/>: GROUP BY по значеннях полів і агрегати над ними.
/// </summary>
/// <remarks>
/// <para>
/// У EAV значення різних полів лежать у різних рядках, тому для кожного поля, що бере участь у групуванні
/// або показниках, запис з'єднується з його значенням через LEFT JOIN по первинному ключу
/// (InstancePkey, PropertyPkey). Ключ унікальний, тож з'єднання не розмножує записи і COUNT(*) рахує саме записи.
/// </para>
/// <para>
/// Результат: колонки K0..Kn (значення групувань) і M0..Mn (показники). Для групування числа за кроком -
/// ще R{i}Min / R{i}Max: фактичні мінімум і максимум значень у діапазоні (група "6 – 8" може містити лише 6).
/// Обирається на одну групу більше за ліміт, щоб знати, чи результат обрізано.
/// </para>
/// </remarks>
internal sealed class RecordAggregateSql : RecordSqlBuilder
{
  private readonly Dictionary<Guid, string> _joins = [];
  private readonly StringBuilder _joinClauses = new();

  private RecordAggregateSql()
  {
  }

  public static RecordAggregateSql Build(EntityConfig config, AggregateQuery query)
  {
    var builder = new RecordAggregateSql();
    builder.Sql = builder.BuildSql(config, query);
    return builder;
  }

  private string BuildSql(EntityConfig config, AggregateQuery query)
  {
    var where = WhereClause(config, query.Filters, query.Search);

    var timeZone = query.TimeZone is null
      ? null
      : AddParameter("TimeZone", SqlServerTimeZoneId(query.TimeZone), DbType.String);

    var keys = query.Dimensions.Select(d => KeyExpression(d, timeZone)).ToList();
    var metrics = query.Metrics.Select(MetricExpression).ToList();

    AddParameter("Limit", query.Limit + 1, DbType.Int32);

    // Межі фактичних значень у діапазонах: підпис "6 – 8" сам не каже, чи там 6, 7 чи 8.
    var ranges = query.Dimensions
      .Select((d, i) => (Dimension: d, Index: i))
      .Where(x => x.Dimension.Step != null)
      .SelectMany(x =>
      {
        var value = ValueExpression(x.Dimension.Field);
        return new[] { $"MIN({value}) AS [R{x.Index}Min]", $"MAX({value}) AS [R{x.Index}Max]" };
      });

    var select = keys.Select((k, i) => $"{k} AS [K{i}]")
      .Concat(metrics.Select((m, i) => $"{m} AS [M{i}]"))
      .Concat(ranges);

    var sql = new StringBuilder();
    sql.AppendLine("SET NOCOUNT ON;");
    sql.AppendLine();
    sql.Append(Declarations);
    sql.AppendLine();
    sql.AppendLine("SELECT TOP (@Limit)");
    sql.AppendLine("    " + string.Join(",\n    ", select));
    sql.AppendLine("FROM [dbo].[EntityInstances] i");
    sql.Append(_joinClauses);
    sql.AppendLine($"WHERE {where}");

    if (keys.Count > 0)
    {
      sql.AppendLine($"GROUP BY {string.Join(", ", keys)}");
      sql.AppendLine($"ORDER BY {OrderBy(query, keys)}");
    }

    sql.AppendLine("OPTION (RECOMPILE);");
    return sql.ToString();
  }

  /// <summary>Значення групування: значення поля, діапазон числа або початок інтервалу дати.</summary>
  private string KeyExpression(AggregateDimension dimension, string? timeZone)
  {
    var value = ValueExpression(dimension.Field);

    if (dimension.Interval is { } interval)
    {
      // Дата й час зберігаються в UTC; межі днів/місяців рахуються в заданому часовому поясі.
      var hasTime = dimension.Field.SystemField != null || dimension.Field.Type == PropertyDataType.DateTime;
      if (hasTime && timeZone != null)
        value = $"CAST(({value} AT TIME ZONE 'UTC') AT TIME ZONE {timeZone} AS DATETIME2)";

      return IntervalStart(value, interval);
    }

    if (dimension.Step is { } step)
    {
      var stepParameter = AddParameter(NextName("Step"), step, DbType.Decimal);
      return $"FLOOR({value} / {stepParameter}) * {stepParameter}";
    }

    return value;
  }

  /// <summary>
  /// Початок інтервалу. Без DATETRUNC (лише SQL Server 2022), щоб працювало і на старіших версіях.
  /// Тиждень - з понеділка: 1900-01-01 був понеділком, тож ділимо кількість днів від нього на 7.
  /// </summary>
  private static string IntervalStart(string value, DateInterval interval) => interval switch
  {
    DateInterval.Hour => $"DATEADD(HOUR, DATEDIFF(HOUR, CAST('2000-01-01' AS DATETIME2), {value}), CAST('2000-01-01' AS DATETIME2))",
    DateInterval.Day => $"CAST(CAST({value} AS DATE) AS DATETIME2)",
    DateInterval.Week => $"CAST(DATEADD(DAY, DATEDIFF(DAY, CAST('1900-01-01' AS DATE), CAST({value} AS DATE)) / 7 * 7, CAST('1900-01-01' AS DATE)) AS DATETIME2)",
    DateInterval.Month => $"CAST(DATEFROMPARTS(YEAR({value}), MONTH({value}), 1) AS DATETIME2)",
    DateInterval.Quarter => $"CAST(DATEFROMPARTS(YEAR({value}), (DATEPART(QUARTER, {value}) - 1) * 3 + 1, 1) AS DATETIME2)",
    _ => $"CAST(DATEFROMPARTS(YEAR({value}), 1, 1) AS DATETIME2)"
  };

  private string MetricExpression(AggregateMetric metric)
  {
    if (metric.Field is null)
      return "COUNT(*)";

    var value = ValueExpression(metric.Field);

    return metric.Function switch
    {
      AggregateFunction.Count => $"COUNT({value})",
      AggregateFunction.Sum => $"SUM({value})",
      AggregateFunction.Avg => $"AVG({value})",
      AggregateFunction.Min => $"MIN({value})",
      _ => $"MAX({value})"
    };
  }

  /// <summary>
  /// Порядок груп: за значеннями (переліки - у порядку варіантів) або за першим показником.
  /// </summary>
  private string OrderBy(AggregateQuery query, IReadOnlyList<string> keys)
  {
    var byKeys = query.Dimensions.Select((d, i) =>
      d.Field.Property is { Type: PropertyDataType.Enum, Options.Count: > 0 } property && d.Interval is null && d.Step is null
        ? EnumOrder(property, keys[i])
        : keys[i]);

    var keyOrder = string.Join(", ", byKeys);

    return query.Order switch
    {
      AggregateOrder.ValueDesc => $"[M0] DESC, {keyOrder}",
      AggregateOrder.ValueAsc => $"[M0] ASC, {keyOrder}",
      _ => keyOrder
    };
  }

  /// <summary>Значення поля для запису: системна колонка або колонка типу з приєднаного значення.</summary>
  private string ValueExpression(RecordField field)
  {
    if (field.Property is not { } property)
      return SystemColumn(field);

    return $"{Join(property)}.{ValueColumn(property.Type)}";
  }

  /// <summary>LEFT JOIN значення поля (один на поле, навіть якщо воно і в групуванні, і в показниках).</summary>
  private string Join(EntityPropertyConfig property)
  {
    if (_joins.TryGetValue(property.Id, out var alias))
      return alias;

    alias = $"j{_joins.Count}";
    _joins[property.Id] = alias;

    _joinClauses.AppendLine(
      $"LEFT JOIN {ValuesTable} {alias} ON {alias}.[InstancePkey] = i.[Pkey] AND {alias}.[PropertyPkey] = {PropertyVariable(property)}");

    return alias;
  }

  /// <summary>SQL Server знає часові пояси за ідентифікаторами Windows ("FLE Standard Time").</summary>
  private static string SqlServerTimeZoneId(TimeZoneInfo timeZone)
  {
    if (timeZone.HasIanaId && TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZone.Id, out var windowsId))
      return windowsId;

    return timeZone.Id;
  }
}
