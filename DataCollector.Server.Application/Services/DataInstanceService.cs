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
      PageSize = query.PageSize
    }, cancellationToken);
  }

  public async Task<PagedResult<DataInstanceDto>> Query(
    string config,
    QueryDataInstancesCommand command,
    CancellationToken cancellationToken = default)
  {
    var entity = await entityConfigRepository.Load(config);

    var filters = (command.Filters ?? [])
      .Select(f => RecordFilter.Create(
        RecordField.Resolve(entity, f.Field),
        f.Operator.ToFilterOperator(),
        f.Value?.ToRawValue(f.Field),
        f.ValueTo?.ToRawValue(f.Field),
        f.Values?.Select(v => v.ToRawValue(f.Field)).ToList()))
      .ToList();

    var query = new RecordQuery(
      filters,
      command.Search,
      RecordField.Resolve(entity, string.IsNullOrWhiteSpace(command.SortBy) ? RecordField.CreatedAtKey : command.SortBy),
      command.SortDescending,
      Math.Max(1, command.PageNumber),
      Math.Clamp(command.PageSize, 1, MaxPageSize));

    var (items, totalCount) = await entityInstanceRepository.Query(entity, query, cancellationToken);

    return new PagedResult<DataInstanceDto>
    {
      Items = items.Select(i => i.ToDto()).ToList(),
      TotalCount = totalCount,
      PageNumber = query.PageNumber,
      PageSize = query.PageSize
    };
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
