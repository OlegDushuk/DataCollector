using System.Data;
using System.Text;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.DataAccess.Querying;

/// <summary>
/// Параметризований T-SQL для <see cref="RecordQuery"/>: кількість записів, сторінка і значення полів цих записів.
/// Умови фільтра і пошуку - у <see cref="RecordSqlBuilder"/>.
/// </summary>
internal sealed class RecordQuerySql : RecordSqlBuilder
{
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
    var where = WhereClause(config, query.Filters, query.Search);
    var (sortJoin, orderBy) = Sorting(query);

    AddParameter("Offset", (query.PageNumber - 1) * query.PageSize, DbType.Int32);
    AddParameter("PageSize", query.PageSize, DbType.Int32);

    var sql = new StringBuilder();
    // Проєкція готується до Declarations, бо оголошує змінні з ключами вибраних полів.
    var valuesFilter = ValuesFilter(query.Fields);

    sql.AppendLine("SET NOCOUNT ON;");
    sql.AppendLine();
    sql.AppendLine("-- Внутрішні ключі моделі та полів: далі всі зв'язки через них.");
    sql.Append(Declarations);
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
      """);
    sql.Append("INNER JOIN [dbo].[EntityPropertyConfigs] p ON p.[Pkey] = v.[PropertyPkey]");
    sql.AppendLine(valuesFilter is null ? ";" : $"\n{valuesFilter};");

    return sql.ToString();
  }

  /// <summary>
  /// Значення лише вибраних полів. (InstancePkey, PropertyPkey) - первинний ключ, тож це той самий пошук
  /// по записах сторінки, але з БД читається і передається менше рядків.
  /// </summary>
  private string? ValuesFilter(IReadOnlyList<EntityPropertyConfig>? fields)
  {
    if (fields is null)
      return null;

    return fields.Count == 0
      ? "WHERE 1 = 0"
      : $"WHERE v.[PropertyPkey] IN ({string.Join(", ", fields.Select(PropertyVariable))})";
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
      return (null, $"{SystemColumn(query.SortField)} {direction}, i.[Pkey] {direction}");

    var join = $"LEFT JOIN {ValuesTable} s ON s.[InstancePkey] = i.[Pkey] AND s.[PropertyPkey] = {PropertyVariable(property)}";

    var sortExpression = property.Type == PropertyDataType.Enum && property.Options.Count > 0
      ? EnumOrder(property, "s.[ValueText]")
      : $"s.{ValueColumn(property.Type)}";

    return (join, $"{sortExpression} {direction}, {tieBreak}");
  }
}
