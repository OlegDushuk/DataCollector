using DataCollector.Server.Domain.Entities;

namespace DataCollector.Server.Application.Interfaces.Repositories;

public interface IEntityPropertyConfigRepository
{
  Task CreateMany(Guid entityConfigId, IEnumerable<EntityPropertyConfig> properties);
  Task Update(EntityPropertyConfig property);
  Task Delete(Guid propertyId);
}
