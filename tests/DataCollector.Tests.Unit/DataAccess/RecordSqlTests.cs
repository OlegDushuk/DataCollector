using DataCollector.Server.DataAccess.Querying;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Tests.Unit.DataAccess;

/// <summary>
/// Динамічний SQL пошуку й агрегації: значення користувача - лише параметрами, у тексті - тільки фіксовані фрагменти
/// й ідентифікатори з білого списку. Це основа захисту від SQL-ін'єкцій.
/// </summary>
public class RecordSqlTests
{
  private const string Injection = "x'; DROP TABLE [dbo].[Users]; --";
  private readonly Server.Domain.Entities.EntityConfig _orders = TestModels.Orders();

  private RecordQuery Query(IReadOnlyList<RecordFilter>? filters = null, string? search = null, string sort = "createdAt",
    IReadOnlyList<Server.Domain.Entities.EntityPropertyConfig>? fields = null)
    => new(filters ?? [], search, RecordField.Resolve(_orders, sort), true, 3, 25, fields);

  [Fact]
  public void Search_And_FilterValues_AreParameters_NotSqlText()
  {
    var filter = RecordFilter.Create(RecordField.Resolve(_orders, "number"), FilterOperator.Contains, Injection);

    var command = RecordQuerySql.Build(_orders, Query([filter], search: Injection));

    Assert.DoesNotContain("DROP TABLE", command.Sql);
    Assert.DoesNotContain(Injection, command.Sql);
    Assert.Contains(command.Parameters, p => p.Value is string s && s.Contains("DROP TABLE"));
  }

  [Fact]
  public void FieldIds_AreParameters_AndPagingIsParameterized()
  {
    var filter = RecordFilter.Create(RecordField.Resolve(_orders, "price"), FilterOperator.GreaterThan, "100");

    var command = RecordQuerySql.Build(_orders, Query([filter]));

    Assert.DoesNotContain(_orders.FindProperty("price")!.Id.ToString(), command.Sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains(command.Parameters, p => p.Value is Guid id && id == _orders.FindProperty("price")!.Id);
    Assert.Contains(command.Parameters, p => p.Name == "Offset" && (int)p.Value! == 50);
    Assert.Contains(command.Parameters, p => p.Name == "PageSize" && (int)p.Value! == 25);
    Assert.Contains(command.Parameters, p => p.Value is decimal d && d == 100m);
  }

  [Fact]
  public void NumberFilter_UsesTypedColumn_ForIndex()
  {
    var filter = RecordFilter.Create(RecordField.Resolve(_orders, "price"), FilterOperator.GreaterThan, "100");

    var sql = RecordQuerySql.Build(_orders, Query([filter])).Sql;

    Assert.Contains("[ValueNumber]", sql);
    Assert.Contains("OPTION (RECOMPILE)", sql);
  }

  [Fact]
  public void Projection_ReturnsOnlySelectedFields()
  {
    var price = _orders.FindProperty("price")!;

    var command = RecordQuerySql.Build(_orders, Query(fields: [price]));

    Assert.Contains(command.Parameters, p => p.Value is Guid id && id == price.Id);
    Assert.DoesNotContain(command.Parameters, p => p.Value is Guid id && id == _orders.FindProperty("number")!.Id);
  }

  [Fact]
  public void Query_ReturnsRowVersion_ForOptimisticConcurrency()
  {
    Assert.Contains("CAST(i.[RowVersion] AS BIGINT)", RecordQuerySql.Build(_orders, Query()).Sql);
  }

  [Fact]
  public void Aggregate_GroupAndMetric_ValuesAreParameters()
  {
    var query = new AggregateQuery(
      [RecordFilter.Create(RecordField.Resolve(_orders, "status"), FilterOperator.In, null, values: ["new", Injection])],
      Injection,
      [AggregateDimension.Create(RecordField.Resolve(_orders, "createdAt"), DateInterval.Month)],
      [AggregateMetric.Create(AggregateFunction.Sum, RecordField.Resolve(_orders, "price"))],
      AggregateOrder.Keys, 100, AggregateQuery.ResolveTimeZone("Europe/Kyiv"));

    var command = RecordAggregateSql.Build(_orders, query);

    Assert.DoesNotContain("DROP TABLE", command.Sql);
    Assert.Contains("SUM(", command.Sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains(command.Parameters, p => p.Value is string s && s.Contains("DROP TABLE"));
  }
}
