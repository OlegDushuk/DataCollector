using Dapper;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Common;
using DataCollector.Server.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

public class EntityPropertyConfigRepository : RepositoryBase, IEntityPropertyConfigRepository
{
  public EntityPropertyConfigRepository(IConfiguration config)
    : base(config, "EntityPropertyConfigs")
  {
  }

  public async Task CreateManyAsync(Guid entityConfigId, IEnumerable<EntityPropertyConfig> properties)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Properties", TableValuedParameters.ToPropertyList(entityConfigId, properties));
    
    await ExecuteAsync("CreateMany", parameters);
  }

  public async Task Update(EntityPropertyConfig property)
  {
    await ExecuteAsync("Update", new { property.Id, property.Key, property.Name });
  }

  public async Task Delete(Guid propertyId)
  {
    await ExecuteAsync("Delete", new { Id = propertyId });
  }
}
