using DataCollector.Server.Domain.Entities;

namespace DataCollector.Server.Application.Interfaces.Repositories;

public interface IEntityInstanceRepository
{
  /// <summary>
  /// Сторінка записів. <paramref name="sortPropertyId"/> = null - сортування за датою створення.
  /// </summary>
  Task<(List<EntityInstance> Items, int TotalCount)> GetPage(
    EntityConfig config,
    int pageNumber,
    int pageSize,
    Guid? sortPropertyId,
    bool sortDescending);

  /// <summary>Повертає null, якщо запису немає або він належить іншій моделі.</summary>
  Task<EntityInstance?> GetById(EntityConfig config, Guid id);

  Task Create(EntityInstance instance);
  Task Update(EntityInstance instance);
  Task Delete(Guid id);
}
