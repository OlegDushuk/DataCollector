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

  /// <summary>Групування записів і показники по групах (з тими ж умовами фільтра).</summary>
  Task<AggregateResultDto> Aggregate(
    string config,
    AggregateDataInstancesCommand command,
    CancellationToken cancellationToken = default);

  /// <summary>Сторінка записів з умовами фільтра, пошуком і сортуванням.</summary>
  Task<PagedResult<DataInstanceDto>> Query(
    string config,
    QueryDataInstancesCommand command,
    CancellationToken cancellationToken = default);
  Task<DataInstanceDto> Get(string config, Guid instanceId);
  Task<Guid> CreateInstance(string config, CreateDataInstanceCommand command);

  /// <summary>Змінює передані поля; повертає нову версію запису.</summary>
  Task<long> UpdateInstance(string config, Guid instanceId, UpdateDataInstanceCommand command);

  /// <param name="expectedVersion">Версія, яку бачив клієнт; null - без перевірки.</param>
  Task DeleteInstance(string config, Guid instanceId, long? expectedVersion = null);
}
