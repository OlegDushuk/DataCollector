using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Application.Services.Internal;
using DataCollector.Server.Domain.Entities;

namespace DataCollector.Server.Application.Services;

public class DataConfigService(
  IEntityConfigRepository entityConfigRepository,
  IEntityPropertyConfigRepository entityPropertyConfigRepository
) : IDataConfigService
{
  public Task<List<EntityConfigListItemDto>> GetAll()
  {
    return entityConfigRepository.GetAll();
  }

  public async Task<EntityConfigDto> Get(string config)
  {
    var entity = await entityConfigRepository.Load(config);
    return entity.ToDto();
  }

  public async Task<Guid> CreateDataConfig(CreateEntityConfigCommand command)
  {
    var config = new EntityConfig(command.Key, command.Name);

    foreach (var prop in command.Properties)
      config.AddProperty(prop.Key, prop.Name, prop.DataType.ToDomain());

    await entityConfigRepository.Create(config);
    return config.Id;
  }
  
  public async Task EditDataConfig(string config, EditEntityConfigCommand command)
  {
    var entity = await entityConfigRepository.Load(config);

    if (command.Key != null)
      entity.ChangeKey(command.Key);
    
    if (command.Name != null)
      entity.ChangeName(command.Name);
    
    await entityConfigRepository.Update(entity);
  }

  public async Task DeleteDataConfig(string config)
  {
    var entity = await entityConfigRepository.Load(config);
    await entityConfigRepository.Delete(entity.Id);
  }

  public async Task<List<Guid>> AddProperties(string config, AddPropertiesCommand command)
  {
    var entity = await entityConfigRepository.Load(config);

    var added = command.Properties
      .Select(p => entity.AddProperty(p.Key, p.Name, p.DataType.ToDomain()))
      .ToList();

    if (added.Count == 0)
      return [];

    await entityPropertyConfigRepository.CreateManyAsync(entity.Id, added);
    return added.Select(p => p.Id).ToList();
  }

  public async Task EditProperty(string config, Guid propertyId, EditPropertyCommand command)
  {
    var entity = await entityConfigRepository.Load(config);
    EnsurePropertyExists(entity, propertyId);

    var property = entity.ChangeProperty(propertyId, command.Key, command.Name);
    await entityPropertyConfigRepository.Update(property);
  }

  public async Task DeleteProperty(string config, Guid propertyId)
  {
    var entity = await entityConfigRepository.Load(config);
    EnsurePropertyExists(entity, propertyId);

    entity.RemoveProperty(propertyId);
    await entityPropertyConfigRepository.Delete(propertyId);
  }

  private static void EnsurePropertyExists(EntityConfig entity, Guid propertyId)
  {
    if (entity.FindProperty(propertyId) is null)
      throw new NotFoundException("Поле не знайдене");
  }
}
