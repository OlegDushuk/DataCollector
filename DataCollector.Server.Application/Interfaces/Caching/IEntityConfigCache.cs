using DataCollector.Server.Domain.Entities;

namespace DataCollector.Server.Application.Interfaces.Caching;

/// <summary>
/// Моделі (з полями) для операцій із записами: читання, агрегація, експорт, створення й зміна записів.
/// Модель потрібна кожному такому запиту, а змінюється рідко, тож тримається в пам'яті.
/// </summary>
/// <remarks>
/// Повернута модель спільна для паралельних запитів - її не можна змінювати. Зміни моделі
/// (DataConfigService) читають її з БД напряму, а після запису викликають <see cref="Invalidate"/>.
/// </remarks>
public interface IEntityConfigCache
{
  /// <summary>Модель за Id або ключем; немає - NotFoundException.</summary>
  Task<EntityConfig> Get(string config);

  /// <summary>Модель або її поля змінились - наступні запити прочитають їх з БД.</summary>
  void Invalidate();
}
