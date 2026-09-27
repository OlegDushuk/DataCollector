using System.Data;
using Dapper;
using DataCollector.Server.Application.ExternalContracts.Enums;
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

  public async Task CreateManyAsync(EntityConfig entityConfig)
  {
    var table = new DataTable();
    
    table.Columns.Add("Id", typeof(Guid));
    table.Columns.Add("EntityConfigId", typeof(Guid));
    table.Columns.Add("Key", typeof(string));
    table.Columns.Add("Name", typeof(string));
    table.Columns.Add("DataType", typeof(int));
    table.Columns.Add("CreatedAt", typeof(DateTime));
    
    entityConfig.Properties.ToList().ForEach(prop =>
    {
      table.Rows.Add(prop.Id,
        entityConfig.Id,
        prop.Key,
        prop.Name,
        (int)prop.Type,
        prop.CreatedAt);
    });
    
    var parameters = new DynamicParameters();
    parameters.Add("Properties", table.AsTableValuedParameter("dbo.EntityPropertyList"));
    
    await ExecuteAsync("CreateMany", parameters);
  }
}