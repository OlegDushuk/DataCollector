using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;

namespace DataCollector.Server.Application.Services;

public class DataConfigService(
  IEntityConfigRepository entityConfigRepository,
  IEntityPropertyConfigRepository entityPropertyConfigRepository
) : IDataConfigService
{
  public async Task CreateDataConfig(CreateEntityConfigCommand command)
  {
    var config = new EntityConfig(command.Key, command.Name);
    command.Properties.ForEach(
      prop => config.AddProperty(
        prop.Key,
        prop.Name,
        (PropertyDataType)prop.DataType));
    
    await entityConfigRepository.Create(config);
    if (config.Properties.Count != 0)
      await entityPropertyConfigRepository.CreateManyAsync(config);
  }
  
  public async Task EditDataConfig(EditEntityConfigCommand command)
  {
    var configData = await entityConfigRepository.GetById(command.EntityConfigId);
    if (configData == null)
      return;
    
    var config = new EntityConfig(
      configData.Key,
      configData.Name,
      configData.Id,
      configData.CreatedAt);
    
    if (command.Key != null)
      config.ChangeKey(command.Key);
    
    if (command.Name != null)
      config.ChangeName(command.Name);
    
    await entityConfigRepository.Update(config);
  }
}