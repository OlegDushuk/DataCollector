using DataCollector.Server.Application.ExternalContracts.Models;

namespace DataCollector.Server.Application.ExternalContracts.Interfaces;

/// <summary>
/// Керування моделями даних та їх полями.
/// Параметр <c>config</c> - Id моделі або її ключ.
/// </summary>
public interface IDataConfigService
{
  Task<List<EntityConfigListItemDto>> GetAll();
  Task<EntityConfigDto> Get(string config);
  Task<Guid> CreateDataConfig(CreateEntityConfigCommand command);
  Task EditDataConfig(string config, EditEntityConfigCommand command);
  Task DeleteDataConfig(string config);

  Task<List<Guid>> AddProperties(string config, AddPropertiesCommand command);
  Task EditProperty(string config, Guid propertyId, EditPropertyCommand command);
  Task DeleteProperty(string config, Guid propertyId);
}
