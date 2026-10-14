using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Domain.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DataCollector.Server.Application.Services;

public class ApiKeyService(
  IApiKeyRepository apiKeys,
  IMemoryCache cache,
  ILogger<ApiKeyService> logger
) : IApiKeyService
{
  /// <summary>Скільки тримати в пам'яті результат перевірки ключа: пристрій може надсилати дані щосекунди.</summary>
  private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(1);

  /// <summary>Як часто оновлювати «останнє використання» - не на кожен запит.</summary>
  private static readonly TimeSpan UsageResolution = TimeSpan.FromMinutes(5);

  public async Task<List<ApiKeyDto>> GetAll()
  {
    return (await apiKeys.GetAll())
      .OrderBy(r => r.Key.IsActive ? 0 : 1)
      .ThenByDescending(r => r.Key.CreatedAt)
      .Select(r => r.Key.ToDto(r.CreatedByName))
      .ToList();
  }

  public async Task<CreatedApiKeyDto> Create(CreateApiKeyCommand command, Guid? createdByUserId)
  {
    var (key, plain) = ApiKey.Generate(command.Name, UserRoles.Parse(command.Role), createdByUserId);
    await apiKeys.Create(key);

    logger.LogInformation("Створено API-ключ {KeyName} ({Prefix}, роль {Role})", key.Name, key.Prefix, key.Role);
    return new CreatedApiKeyDto { Key = key.ToDto(createdBy: null), PlainKey = plain };
  }

  public async Task Revoke(Guid id)
  {
    var key = await apiKeys.GetById(id) ?? throw new NotFoundException("API-ключ не знайдений");
    if (!key.IsActive)
      return;

    await apiKeys.Revoke(id, DateTime.UtcNow);

    // Ключ перестає працювати одразу, а не після закінчення кешу.
    cache.Remove(CacheKey(key.KeyHash));
    logger.LogInformation("Відкликано API-ключ {KeyName} ({Prefix})", key.Name, key.Prefix);
  }

  public async Task<ApiKeyIdentity?> Authenticate(string plainKey)
  {
    if (!ApiKey.LooksValid(plainKey))
      return null;

    var hash = ApiKey.Hash(plainKey);

    var entry = await cache.GetOrCreateAsync(CacheKey(hash), async e =>
    {
      e.AbsoluteExpirationRelativeToNow = CacheTime;
      var key = await apiKeys.GetByHash(hash);
      return key is { IsActive: true } ? new CachedKey(key.Id, key.Name, key.Role, key.LastUsedAt) : null;
    });

    if (entry is null)
      return null;

    var now = DateTime.UtcNow;
    if (entry.LastUsedAt is null || now - entry.LastUsedAt > UsageResolution)
    {
      entry.LastUsedAt = now;
      await apiKeys.MarkUsed(entry.Id, now);
    }

    return new ApiKeyIdentity(entry.Id, entry.Name, entry.Role);
  }

  private static string CacheKey(byte[] hash) => "auth:key:" + Convert.ToHexString(hash);

  private sealed class CachedKey(Guid id, string name, UserRole role, DateTime? lastUsedAt)
  {
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public UserRole Role { get; } = role;
    public DateTime? LastUsedAt { get; set; } = lastUsedAt;
  }
}
