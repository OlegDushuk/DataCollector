using Dapper;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Common;
using DataCollector.Server.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

public class EntityConfigRepository
  : RepositoryBase, IEntityConfigRepository
{
  public EntityConfigRepository(IConfiguration config)
    : base(config, "EntityConfigs")
  {
  }
  
  public async Task Create(EntityConfig model)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", model.Id);
    parameters.Add("CreatedAt", model.CreatedAt);
    parameters.Add("Key", model.Key);
    parameters.Add("Name", model.Name);

    await ExecuteAsync("Create", parameters);
  }

  public async Task<EntityConfig?> GetById(Guid id)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", id);
    
    return await QueryAsync<EntityConfig>("GetById", parameters);
  }
  
  public async Task Update(EntityConfig model)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", model.Id);
    parameters.Add("Key", model.Key);
    parameters.Add("Name", model.Name);
    
    await ExecuteAsync("Update", parameters);
  }
}