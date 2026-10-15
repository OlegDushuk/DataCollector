using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Tests.Unit.Domain;

/// <summary>Умови фільтра, запит сторінки і запит агрегації: перевірка на рівні домену до побудови SQL.</summary>
public class RecordQueryTests
{
  private readonly Server.Domain.Entities.EntityConfig _orders = TestModels.Orders();

  [Theory]
  [InlineData("number", FilterOperator.Contains, "ORD")]
  [InlineData("price", FilterOperator.GreaterThan, "1")]
  [InlineData("paid", FilterOperator.Equals, "true")]
  [InlineData("status", FilterOperator.In, null)]
  [InlineData("createdAt", FilterOperator.LessOrEqual, "2026-10-01T00:00:00Z")]
  public void AllowedOperator_Accepted(string field, FilterOperator op, string? value)
  {
    var filter = RecordFilter.Create(RecordField.Resolve(_orders, field), op, value, values: ["new"]);

    Assert.Equal(op, filter.Operator);
  }

  [Theory]
  [InlineData("price", FilterOperator.Contains)]
  [InlineData("paid", FilterOperator.GreaterThan)]
  [InlineData("createdAt", FilterOperator.IsEmpty)]
  public void OperatorNotSuitableForType_Rejected(string field, FilterOperator op)
  {
    Assert.Throws<DomainValidationException>(() => RecordFilter.Create(RecordField.Resolve(_orders, field), op, "1"));
  }

  [Fact]
  public void FilterValue_IsParsedByFieldType()
  {
    var filter = RecordFilter.Create(RecordField.Resolve(_orders, "price"), FilterOperator.Between, "10,5", "20");

    Assert.Equal([10.5m, 20m], filter.Values);
  }

  [Fact]
  public void FilterValue_InvalidForType_Rejected()
  {
    Assert.Throws<DomainValidationException>(() =>
      RecordFilter.Create(RecordField.Resolve(_orders, "price"), FilterOperator.Equals, "дорого"));
  }

  [Fact]
  public void UnknownField_Rejected()
  {
    Assert.Throws<DomainValidationException>(() => RecordField.Resolve(_orders, "discount"));
  }

  [Fact]
  public void Query_TooManyFilters_Rejected()
  {
    var field = RecordField.Resolve(_orders, "price");
    var filters = Enumerable.Range(0, RecordQuery.MaxFilters + 1)
      .Select(i => RecordFilter.Create(field, FilterOperator.GreaterThan, i.ToString()))
      .ToList();

    Assert.Throws<DomainValidationException>(() => new RecordQuery(filters, null, field, false, 1, 10));
  }

  [Theory]
  [InlineData(0, 10)]
  [InlineData(1, 0)]
  public void Query_InvalidPaging_Rejected(int page, int size)
  {
    Assert.Throws<DomainValidationException>(() =>
      new RecordQuery([], null, RecordField.Resolve(_orders, "createdAt"), true, page, size));
  }

  [Fact]
  public void Aggregate_SumOfTextField_Rejected()
  {
    Assert.Throws<DomainValidationException>(() =>
      AggregateMetric.Create(AggregateFunction.Sum, RecordField.Resolve(_orders, "number")));
  }

  [Fact]
  public void Aggregate_NeedsAtLeastOneMetric_AndAtMostTwoDimensions()
  {
    var status = AggregateDimension.Create(RecordField.Resolve(_orders, "status"));
    var count = AggregateMetric.Create(AggregateFunction.Count, null);

    Assert.Throws<DomainValidationException>(() => new AggregateQuery([], null, [status], [], AggregateOrder.Keys, 100, null));
    Assert.Throws<DomainValidationException>(() => new AggregateQuery([], null, [status, status, status], [count], AggregateOrder.Keys, 100, null));
    Assert.Throws<DomainValidationException>(() => new AggregateQuery([], null, [status], [count], AggregateOrder.Keys, AggregateQuery.MaxLimit + 1, null));
  }

  [Fact]
  public void Aggregate_TimeZone_ResolvedByIanaId()
  {
    Assert.NotNull(AggregateQuery.ResolveTimeZone("Europe/Kyiv"));
    Assert.Null(AggregateQuery.ResolveTimeZone(null));
  }
}
