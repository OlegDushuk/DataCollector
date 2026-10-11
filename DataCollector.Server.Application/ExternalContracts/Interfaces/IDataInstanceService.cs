using DataCollector.Server.Application.ExternalContracts.Models;

namespace DataCollector.Server.Application.ExternalContracts.Interfaces;

/// <summary>
/// Робота із записами моделі. Параметр <c>config</c> - Id моделі або її ключ.
/// </summary>
public interface IDataInstanceService
{
  Task<PagedResult<DataInstanceDto>> GetPage(
    string config,
    GetDataInstancesQuery query,
    CancellationToken cancellationToken = default);

  /// <summary>Сторінка записів з умовами фільтра, пошуком і сортуванням.</summary>
  Task<PagedResult<DataInstanceDto>> Query(
    string config,
    QueryDataInstancesCommand command,
    CancellationToken cancellationToken = default);
  Task<DataInstanceDto> Get(string config, Guid instanceId);
  Task<Guid> CreateInstance(string config, CreateDataInstanceCommand command);
  Task UpdateInstance(string config, Guid instanceId, UpdateDataInstanceCommand command);
  Task DeleteInstance(string config, Guid instanceId);
}
