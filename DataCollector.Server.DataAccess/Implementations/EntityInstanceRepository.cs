using Dapper;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Common;
using DataCollector.Server.DataAccess.Querying;
using DataCollector.Server.DataAccess.Rows;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Querying;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

public class EntityInstanceRepository : RepositoryBase, IEntityInstanceRepository
{
  public EntityInstanceRepository(IConfiguration config)
    : base(config, "EntityInstances")
  {
  }

  public Task<(List<EntityInstance> Items, int TotalCount)> Query(
    EntityConfig config,
    RecordQuery query,
    CancellationToken cancellationToken = default)
  {
    // Умови фільтра довільні, тому SQL збирається під запит (параметризовано), а не береться з процедури.
    var command = RecordQuerySql.Build(config, query);

    var parameters = new DynamicParameters();
    foreach (var parameter in command.Parameters)
      parameters.Add(parameter.Name, parameter.Value, parameter.DbType);

    return QueryMultipleSqlAsync(command.Sql, parameters, async grid =>
    {
      var totalCount = await grid.ReadSingleAsync<int>();
      var rows = (await grid.ReadAsync<EntityInstanceRow>()).ToList();
      var values = (await grid.ReadAsync<EntityPropertyValueRow>()).ToLookup(v => v.EntityInstanceId);

      var items = rows.Select(r => Build(config, r, values[r.Id])).ToList();
      return (items, totalCount);
    }, cancellationToken);
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
      instance.RestorePropertyValue(value.EntityPropertyConfigId, ReadValue(value));

    return instance;
  }

  /// <summary>
  /// У рядку заповнена рівно одна колонка (CHECK у таблиці), тож беремо першу непорожню.
  /// </summary>
  private static object? ReadValue(EntityPropertyValueRow row)
  {
    if (row.ValueText != null)
      return row.ValueText;

    if (row.ValueNumber is { } number)
      return TrimZeros(number);

    if (row.ValueBool is { } flag)
      return flag;

    return row.ValueDate is { } date ? AsUtc(date) : null;
  }

  /// <summary>
  /// DECIMAL(38, 10) повертається з 10 знаками після коми (120.5000000000) - прибираємо зайві нулі,
  /// щоб API віддавало число в тому ж вигляді, у якому його надіслали (120.5).
  /// </summary>
  private static decimal TrimZeros(decimal value) => value / 1.0000000000000000000000000000m;
}
