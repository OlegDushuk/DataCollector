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

    return QueryMultipleSqlAsync(command.Sql, ToParameters(command), async grid =>
    {
      var totalCount = await grid.ReadSingleAsync<int>();
      var rows = (await grid.ReadAsync<EntityInstanceRow>()).ToList();
      var values = (await grid.ReadAsync<EntityPropertyValueRow>()).ToLookup(v => v.EntityInstanceId);

      var items = rows.Select(r => Build(config, r, values[r.Id])).ToList();
      return (items, totalCount);
    }, cancellationToken);
  }

  public async Task<AggregateResult> Aggregate(
    EntityConfig config,
    AggregateQuery query,
    CancellationToken cancellationToken = default)
  {
    var command = RecordAggregateSql.Build(config, query);
    var rows = await QueryRowsSqlAsync(command.Sql, ToParameters(command), cancellationToken);

    // SQL повертає на одну групу більше за ліміт - так видно, що результат обрізано.
    var truncated = rows.Count > query.Limit;
    var hasRanges = query.Dimensions.Any(d => d.Step != null);

    var result = rows
      .Take(query.Limit)
      .Select(row => new AggregateRow(
        query.Dimensions.Select((d, i) => d.NormalizeKey(row[$"K{i}"])).ToList(),
        query.Metrics.Select((m, i) => m.NormalizeValue(row[$"M{i}"])).ToList(),
        hasRanges
          ? query.Dimensions.Select((d, i) => d.Step is null ? null : d.NormalizeRange(row[$"R{i}Min"], row[$"R{i}Max"])).ToList()
          : null))
      .ToList();

    return new AggregateResult(result, truncated);
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

  public async Task<long> Update(EntityInstance instance)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", instance.Id);
    parameters.Add("UpdatedAt", instance.UpdatedAt ?? DateTime.UtcNow);
    // Версія, з якою запис прочитали: процедура порівнює її в момент запису (оптимістична конкурентність).
    parameters.Add("ExpectedVersion", instance.Version);
    parameters.Add("Values", TableValuedParameters.ToValueList(instance));

    return await QueryFirstOrDefaultAsync<long>("Update", parameters);
  }

  public async Task Delete(Guid id, long? expectedVersion = null)
  {
    await ExecuteAsync("Delete", new { Id = id, ExpectedVersion = expectedVersion });
  }

  public async Task<Dictionary<Guid, Guid>> GetOwners(
    IReadOnlyCollection<Guid> ids,
    CancellationToken cancellationToken = default)
  {
    var result = new Dictionary<Guid, Guid>();

    // Частинами: табличний параметр на сотні тисяч рядків - зайве навантаження на один запит.
    foreach (var chunk in ids.Chunk(ImportBatchSize))
    {
      var rows = await QueryMultipleAsync("GetOwners", new { Ids = TableValuedParameters.ToIdList(chunk) },
        async grid => (await grid.ReadAsync<InstanceOwnerRow>()).ToList(), cancellationToken);

      foreach (var row in rows)
        result[row.Id] = row.EntityConfigId;
    }

    return result;
  }

  public Task Import(
    EntityConfig config,
    IReadOnlyList<EntityInstance> created,
    IReadOnlyList<EntityInstance> updated,
    IReadOnlyCollection<Guid> updatedPropertyIds,
    CancellationToken cancellationToken = default)
  {
    return RunInTransaction(async (connection, transaction) =>
    {
      foreach (var chunk in created.Chunk(ImportBatchSize))
      {
        await ExecuteInTransaction(connection, transaction, "CreateMany", new
        {
          EntityConfigId = config.Id,
          Instances = TableValuedParameters.ToInstanceList(chunk),
          Values = TableValuedParameters.ToInstanceValueList(chunk)
        }, cancellationToken);
      }

      foreach (var chunk in updated.Chunk(ImportBatchSize))
      {
        await ExecuteInTransaction(connection, transaction, "UpdateMany", new
        {
          EntityConfigId = config.Id,
          Instances = TableValuedParameters.ToInstanceList(chunk),
          Values = TableValuedParameters.ToInstanceValueList(chunk, updatedPropertyIds)
        }, cancellationToken);
      }
    }, cancellationToken);
  }

  /// <summary>Записів в одному виклику пакетної процедури.</summary>
  private const int ImportBatchSize = 1_000;

  private static DynamicParameters ToParameters(RecordSqlBuilder command)
  {
    var parameters = new DynamicParameters();
    foreach (var parameter in command.Parameters)
      parameters.Add(parameter.Name, parameter.Value, parameter.DbType);

    return parameters;
  }

  private static EntityInstance Build(
    EntityConfig config,
    EntityInstanceRow row,
    IEnumerable<EntityPropertyValueRow> values)
  {
    var instance = new EntityInstance(config, row.Id, AsUtc(row.CreatedAt), AsUtc(row.UpdatedAt), row.Version);

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
