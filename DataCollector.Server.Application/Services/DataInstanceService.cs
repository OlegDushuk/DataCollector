using System.Text.Json;
using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Application.Services.Internal;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.Application.Services;

public class DataInstanceService(
  IEntityConfigRepository entityConfigRepository,
  IEntityInstanceRepository entityInstanceRepository
) : IDataInstanceService
{
  public const int MaxPageSize = 200;

  public Task<PagedResult<DataInstanceDto>> GetPage(
    string config,
    GetDataInstancesQuery query,
    CancellationToken cancellationToken = default)
  {
    // GET - той самий запит без умов фільтра.
    return Query(config, new QueryDataInstancesCommand
    {
      Search = query.Search,
      SortBy = query.SortBy,
      SortDescending = query.SortDescending,
      PageNumber = query.PageNumber,
      PageSize = query.PageSize,
      Fields = string.IsNullOrWhiteSpace(query.Fields)
        ? null
        : query.Fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
    }, cancellationToken);
  }

  public async Task<PagedResult<DataInstanceDto>> Query(
    string config,
    QueryDataInstancesCommand command,
    CancellationToken cancellationToken = default)
  {
    var entity = await entityConfigRepository.Load(config);

    var query = new RecordQuery(
      BuildFilters(entity, command.Filters),
      command.Search,
      RecordField.Resolve(entity, string.IsNullOrWhiteSpace(command.SortBy) ? RecordField.CreatedAtKey : command.SortBy),
      command.SortDescending,
      Math.Max(1, command.PageNumber),
      Math.Clamp(command.PageSize, 1, MaxPageSize),
      BuildFields(entity, command.Fields));

    var (items, totalCount) = await entityInstanceRepository.Query(entity, query, cancellationToken);

    return new PagedResult<DataInstanceDto>
    {
      Items = items.Select(i => i.ToDto(query.Fields)).ToList(),
      TotalCount = totalCount,
      PageNumber = query.PageNumber,
      PageSize = query.PageSize
    };
  }

  public async Task<AggregateResultDto> Aggregate(
    string config,
    AggregateDataInstancesCommand command,
    CancellationToken cancellationToken = default)
  {
    var entity = await entityConfigRepository.Load(config);

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

    var query = new AggregateQuery(
      BuildFilters(entity, command.Filters),
      command.Search,
      dimensions,
      metrics,
      command.OrderBy.ToAggregateOrder(),
      command.Limit,
      AggregateQuery.ResolveTimeZone(command.TimeZone));

    var result = await entityInstanceRepository.Aggregate(entity, query, cancellationToken);
    return query.ToDto(result);
  }

  public async Task<DataInstanceDto> Get(string config, Guid instanceId)
  {
    var entity = await entityConfigRepository.Load(config);
    var instance = await LoadInstance(entity, instanceId);

    return instance.ToDto();
  }

  public async Task<Guid> CreateInstance(string config, CreateDataInstanceCommand command)
  {
    var entity = await entityConfigRepository.Load(config);
    var instance = entity.CreateInstance();

    ApplyValues(instance, command.Values);
    instance.EnsureRequiredFilled();

    await entityInstanceRepository.Create(instance);
    return instance.Id;
  }

  public async Task UpdateInstance(string config, Guid instanceId, UpdateDataInstanceCommand command)
  {
    var entity = await entityConfigRepository.Load(config);
    var instance = await LoadInstance(entity, instanceId);

    ApplyValues(instance, command.Values);
    instance.MarkUpdated();

    await entityInstanceRepository.Update(instance);
  }

  public async Task DeleteInstance(string config, Guid instanceId)
  {
    var entity = await entityConfigRepository.Load(config);
    var instance = await LoadInstance(entity, instanceId);

    await entityInstanceRepository.Delete(instance.Id);
  }

  /// <summary>
  /// Проєкція: ключі -> поля моделі. Системні поля (createdAt, updatedAt) повертаються завжди, тож пропускаються;
  /// невідомий ключ - помилка 400, щоб опечатка не давала мовчки порожню колонку.
  /// </summary>
  private static List<EntityPropertyConfig>? BuildFields(EntityConfig entity, List<string>? keys)
  {
    return keys?
      .Select(key => RecordField.Resolve(entity, key).Property)
      .OfType<EntityPropertyConfig>()
      .ToList();
  }

  private static List<RecordFilter> BuildFilters(EntityConfig entity, List<RecordFilterDto>? filters)
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

  private async Task<EntityInstance> LoadInstance(EntityConfig entity, Guid instanceId)
  {
    return await entityInstanceRepository.GetById(entity, instanceId)
           ?? throw new NotFoundException("Запис не знайдений");
  }

  private static void ApplyValues(EntityInstance instance, Dictionary<string, JsonElement> values)
  {
    // Ключі полів нечутливі до регістру, тож "Price" і "price" в одному запиті - це помилка, а не перезапис.
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var (key, value) in values)
    {
      if (!seen.Add(key))
        throw new DomainValidationException($"Поле '{key}' передано кілька разів");

      instance.SetPropertyValue(key, value.ToRawValue(key));
    }
  }
}
