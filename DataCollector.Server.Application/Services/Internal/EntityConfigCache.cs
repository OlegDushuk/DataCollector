using DataCollector.Server.Application.Interfaces.Caching;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace DataCollector.Server.Application.Services.Internal;

/// <summary>
/// Кеш моделей у пам'яті процесу API. Економить запит до БД (модель + поля) на кожну операцію із записами.
/// </summary>
/// <remarks>
/// <para>
/// Ключі: Id моделі і ключ моделі (без урахування регістру - як у БД). Будь-яка зміна будь-якої моделі
/// скидає весь кеш (через спільний токен): зміни моделей рідкісні, а так не треба відстежувати,
/// під якими ключами модель лежала до перейменування.
/// </para>
/// <para>
/// Термін життя <see cref="Ttl"/> - запобіжник для кількох екземплярів API: зміна моделі через один екземпляр
/// стане видимою в інших не пізніше ніж через цей час.
/// </para>
/// </remarks>
internal sealed class EntityConfigCache(IEntityConfigRepository repository, IMemoryCache cache) : IEntityConfigCache
{
  private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

  private CancellationTokenSource _reset = new();

  public async Task<EntityConfig> Get(string config)
  {
    var cacheKey = CacheKey(config);
    if (cacheKey != null && cache.TryGetValue(cacheKey, out EntityConfig? cached) && cached != null)
      return cached;

    var entity = await repository.Load(config);

    var options = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }
      .AddExpirationToken(new CancellationChangeToken(_reset.Token));

    cache.Set(IdKey(entity.Id), entity, options);
    cache.Set(KeyKey(entity.Key), entity, options);

    return entity;
  }

  public void Invalidate()
  {
    var previous = Interlocked.Exchange(ref _reset, new CancellationTokenSource());
    previous.Cancel();
    previous.Dispose();
  }

  private static string? CacheKey(string config)
  {
    if (string.IsNullOrWhiteSpace(config))
      return null;

    return Guid.TryParse(config, out var id) ? IdKey(id) : KeyKey(config.Trim());
  }

  private static string IdKey(Guid id) => $"entity-config:id:{id}";
  private static string KeyKey(string key) => $"entity-config:key:{key.ToLowerInvariant()}";
}
