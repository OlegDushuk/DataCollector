using Dapper;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Common;
using DataCollector.Server.DataAccess.Rows;
using DataCollector.Server.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

public class EntityInstanceRepository : RepositoryBase, IEntityInstanceRepository
{
  public EntityInstanceRepository(IConfiguration config)
    : base(config, "EntityInstances")
  {
  }

  public Task<(List<EntityInstance> Items, int TotalCount)> GetPage(
    EntityConfig config,
    int pageNumber,
    int pageSize,
    Guid? sortPropertyId,
    bool sortDescending)
  {
    var parameters = new
    {
      EntityConfigId = config.Id,
      PageNumber = pageNumber,
      PageSize = pageSize,
      SortPropertyId = sortPropertyId,
      SortDescending = sortDescending
    };

    return QueryMultipleAsync("GetPage", parameters, async grid =>
    {
      var totalCount = await grid.ReadSingleAsync<int>();
      var rows = (await grid.ReadAsync<EntityInstanceRow>()).ToList();
      var values = (await grid.ReadAsync<EntityPropertyValueRow>()).ToLookup(v => v.EntityInstanceId);

      var items = rows.Select(r => Build(config, r, values[r.Id])).ToList();
      return (items, totalCount);
    });
  }

  public Task<EntityInstance?> GetById(EntityConfig config, Guid id)
  {
    return QueryMultipleAsync("GetById", new { Id = id }, async grid =>
    {
      var row = await grid.ReadFirstOrDefaultAsync<EntityInstanceRow>();
      var values = await grid.ReadAsync<EntityPropertyValueRow>();

      if (row is null || row.EntityConfigId != config.Id)
        return null;

      return Build(config, row, values);
    });
  }

  public async Task Create(EntityInstance instance)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", instance.Id);
    parameters.Add("EntityConfigId", instance.Config.Id);
    parameters.Add("CreatedAt", instance.CreatedAt);
    parameters.Add("Values", TableValuedParameters.ToValueList(instance));

    await ExecuteAsync("Create", parameters);
  }

  public async Task Update(EntityInstance instance)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", instance.Id);
    parameters.Add("UpdatedAt", instance.UpdatedAt ?? DateTime.UtcNow);
    parameters.Add("Values", TableValuedParameters.ToValueList(instance));

    await ExecuteAsync("Update", parameters);
  }

  public async Task Delete(Guid id)
  {
    await ExecuteAsync("Delete", new { Id = id });
  }

  private static EntityInstance Build(
    EntityConfig config,
    EntityInstanceRow row,
    IEnumerable<EntityPropertyValueRow> values)
  {
    var instance = new EntityInstance(config, row.Id, AsUtc(row.CreatedAt), AsUtc(row.UpdatedAt));

    foreach (var value in values)
      instance.RestorePropertyValue(value.EntityPropertyConfigId, value.Id, value.Value, AsUtc(value.CreatedAt));

    return instance;
  }
}
