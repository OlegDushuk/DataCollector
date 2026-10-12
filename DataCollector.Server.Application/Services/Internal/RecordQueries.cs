using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.Application.Services.Internal;

/// <summary>
/// Перетворення команд API на доменні запити. Спільне для таблиці, зведення та експорту,
/// тож експорт отримує рівно ті самі записи і групи, що бачить користувач.
/// </summary>
internal static class RecordQueries
{
  public static List<RecordFilter> Filters(EntityConfig entity, List<RecordFilterDto>? filters)
  {
    return (filters ?? [])
      .Select(f => RecordFilter.Create(
        RecordField.Resolve(entity, f.Field),
        f.Operator.ToFilterOperator(),
        f.Value?.ToRawValue(f.Field),
        f.ValueTo?.ToRawValue(f.Field),
        f.Values?.Select(v => v.ToRawValue(f.Field)).ToList()))
      .ToList();
  }

  /// <summary>
  /// Проєкція: ключі -> поля моделі. Системні поля (createdAt, updatedAt) повертаються завжди, тож пропускаються;
  /// невідомий ключ - помилка 400, щоб опечатка не давала мовчки порожню колонку.
  /// </summary>
  public static List<EntityPropertyConfig>? Fields(EntityConfig entity, IEnumerable<string>? keys)
  {
    return keys?
      .Select(key => RecordField.Resolve(entity, key).Property)
      .OfType<EntityPropertyConfig>()
      .ToList();
  }

  public static RecordField Sort(EntityConfig entity, string? sortBy)
    => RecordField.Resolve(entity, string.IsNullOrWhiteSpace(sortBy) ? RecordField.CreatedAtKey : sortBy);

  public static AggregateQuery Aggregate(EntityConfig entity, AggregateDataInstancesCommand command)
  {
    var dimensions = (command.GroupBy ?? [])
      .Select(d => AggregateDimension.Create(
        RecordField.Resolve(entity, d.Field),
        d.Interval.ToDateInterval(),
        d.Step))
      .ToList();

    // Без показників - кількість записів: найчастіший запит ("скільки записів у кожній групі").
    var metrics = command.Metrics is { Count: > 0 }
      ? command.Metrics
        .Select(m => AggregateMetric.Create(
          m.Function.ToAggregateFunction(),
          string.IsNullOrWhiteSpace(m.Field) ? null : RecordField.Resolve(entity, m.Field)))
        .ToList()
      : [AggregateMetric.Create(AggregateFunction.Count, null)];

    return new AggregateQuery(
      Filters(entity, command.Filters),
      command.Search,
      dimensions,
      metrics,
      command.OrderBy.ToAggregateOrder(),
      command.Limit,
      AggregateQuery.ResolveTimeZone(command.TimeZone));
  }
}
