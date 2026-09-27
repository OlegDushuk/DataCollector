using Dapper;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Common;
using DataCollector.Server.DataAccess.Rows;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

public class EntityConfigRepository
  : RepositoryBase, IEntityConfigRepository
{
  public EntityConfigRepository(IConfiguration config)
    : base(config, "EntityConfigs")
  {
  }

  public async Task<List<EntityConfigListItemDto>> GetAll()
  {
    var items = await QueryListAsync<EntityConfigListItemDto>("GetAll");
    items.ForEach(i => i.CreatedAt = AsUtc(i.CreatedAt));

    return items;
  }

  public Task<EntityConfig?> GetById(Guid id)
  {
    return QueryMultipleAsync("GetById", new { Id = id }, ReadConfig);
  }

  public Task<EntityConfig?> GetByKey(string key)
  {
    return QueryMultipleAsync("GetByKey", new { Key = key }, ReadConfig);
  }
  
  public async Task Create(EntityConfig model)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", model.Id);
    parameters.Add("CreatedAt", model.CreatedAt);
    parameters.Add("Key", model.Key);
    parameters.Add("Name", model.Name);
    parameters.Add("Properties", TableValuedParameters.ToPropertyList(model.Id, model.Properties));

    await ExecuteAsync("Create", parameters);
  }
  
  public async Task Update(EntityConfig model)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", model.Id);
    parameters.Add("Key", model.Key);
    parameters.Add("Name", model.Name);
    
    await ExecuteAsync("Update", parameters);
  }

  public async Task Delete(Guid id)
  {
    await ExecuteAsync("Delete", new { Id = id });
  }

  private static async Task<EntityConfig?> ReadConfig(SqlMapper.GridReader grid)
  {
    var row = await grid.ReadFirstOrDefaultAsync<EntityConfigRow>();
    var propertyRows = await grid.ReadAsync<EntityPropertyConfigRow>();

    if (row is null)
      return null;

    var properties = propertyRows.Select(p => new EntityPropertyConfig(
      p.Key,
      p.Name,
      (PropertyDataType)p.DataType,
      p.Id,
      AsUtc(p.CreatedAt)));

    return new EntityConfig(row.Key, row.Name, row.Id, AsUtc(row.CreatedAt), properties);
  }
}
