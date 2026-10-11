using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.Application.Interfaces.Repositories;

public interface IEntityInstanceRepository
{
  /// <summary>
  /// Сторінка записів, що задовольняють запит, і загальна кількість таких записів.
  /// </summary>
  Task<(List<EntityInstance> Items, int TotalCount)> Query(
    EntityConfig config,
    RecordQuery query,
    CancellationToken cancellationToken = default);

  /// <summary>Повертає null, якщо запису немає або він належить іншій моделі.</summary>
  Task<EntityInstance?> GetById(EntityConfig config, Guid id);

  Task Create(EntityInstance instance);
  Task Update(EntityInstance instance);
  Task Delete(Guid id);
}
