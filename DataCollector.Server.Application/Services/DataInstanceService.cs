using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Application.Services.Internal;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Application.Services;

public class DataInstanceService(
  IEntityConfigRepository entityConfigRepository,
  IEntityInstanceRepository entityInstanceRepository
) : IDataInstanceService
{
  public const int MaxPageSize = 200;

  public async Task<PagedResult<DataInstanceDto>> GetPage(string config, GetDataInstancesQuery query)
  {
    var entity = await entityConfigRepository.Load(config);

    var pageNumber = Math.Max(1, query.PageNumber);
    var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

    Guid? sortPropertyId = null;
    if (!string.IsNullOrWhiteSpace(query.SortBy) &&
        !string.Equals(query.SortBy, "createdAt", StringComparison.OrdinalIgnoreCase))
    {
      sortPropertyId = entity.FindProperty(query.SortBy)?.Id
                       ?? throw new DomainValidationException($"Модель не має поля '{query.SortBy}' для сортування");
    }

    var (items, totalCount) = await entityInstanceRepository.GetPage(
      entity, pageNumber, pageSize, sortPropertyId, query.SortDescending);

    return new PagedResult<DataInstanceDto>
    {
      Items = items.Select(i => i.ToDto()).ToList(),
      TotalCount = totalCount,
      PageNumber = pageNumber,
      PageSize = pageSize
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

  private static void ApplyValues(EntityInstance instance, Dictionary<string, System.Text.Json.JsonElement> values)
  {
    foreach (var (key, value) in values)
      instance.SetPropertyValue(key, value.ToRawValue(key));
  }
}
