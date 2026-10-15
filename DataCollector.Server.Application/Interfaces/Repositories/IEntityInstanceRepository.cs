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

  /// <summary>Групи записів, що задовольняють запит, і показники по кожній групі.</summary>
  Task<AggregateResult> Aggregate(
    EntityConfig config,
    AggregateQuery query,
    CancellationToken cancellationToken = default);

  /// <summary>Повертає null, якщо запису немає або він належить іншій моделі.</summary>
  Task<EntityInstance?> GetById(EntityConfig config, Guid id);

  Task Create(EntityInstance instance);

  /// <summary>
  /// Зберігає зміни, якщо версія запису в сховищі досі <see cref="EntityInstance.Version"/>
  /// (інакше - <see cref="Common.Exceptions.ConflictException"/>). Повертає нову версію.
  /// </summary>
  Task<long> Update(EntityInstance instance);

  /// <param name="expectedVersion">Видалити лише якщо версія запису досі така; null - без перевірки.</param>
  Task Delete(Guid id, long? expectedVersion = null);

  /// <summary>Id моделі, якій належить кожен із наявних записів (відсутніх записів у словнику немає).</summary>
  Task<Dictionary<Guid, Guid>> GetOwners(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

  /// <summary>
  /// Імпорт однією транзакцією: нові записи створюються з усіма значеннями, у наявних записах
  /// замінюються лише значення полів <paramref name="updatedPropertyIds"/> (порожнє значення - очищення).
  /// </summary>
  Task Import(
    EntityConfig config,
    IReadOnlyList<EntityInstance> created,
    IReadOnlyList<EntityInstance> updated,
    IReadOnlyCollection<Guid> updatedPropertyIds,
    CancellationToken cancellationToken = default);
}
