using System.Security.Cryptography;
using System.Text;
using DataCollector.Server.Domain.Common;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Users;

/// <summary>
/// API-ключ для зовнішньої системи або пристрою: передається в заголовку <c>X-Api-Key</c> замість входу користувача.
/// </summary>
/// <remarks>
/// Ключ - 32 випадкові байти (256 біт) у вигляді <c>dc_…</c>. У БД зберігається лише SHA-256 від ключа
/// і перші символи (<see cref="Prefix"/>), щоб ключ можна було впізнати у списку; сам ключ показується
/// один раз - при створенні. Повільний хеш (як для паролів) не потрібен: ключ не можна підібрати перебором.
/// Ключ має роль <see cref="UserRole.Viewer"/> (лише читання) або <see cref="UserRole.Editor"/> (запис даних);
/// адміністрування ключем недоступне.
/// </remarks>
public sealed class ApiKey
{
  public const string KeyPrefix = "dc_";
  private const int PrefixLength = 10;

  public Guid Id { get; }
  public string Name { get; private set; }
  public string Prefix { get; }
  public byte[] KeyHash { get; }
  public UserRole Role { get; }
  public DateTime CreatedAt { get; }
  public Guid? CreatedByUserId { get; }
  public DateTime? LastUsedAt { get; private set; }
  public DateTime? RevokedAt { get; private set; }

  public bool IsActive => RevokedAt is null;

  private ApiKey(Guid id, string name, string prefix, byte[] keyHash, UserRole role, DateTime createdAt,
    Guid? createdByUserId, DateTime? lastUsedAt, DateTime? revokedAt)
  {
    Id = id;
    Name = name;
    Prefix = prefix;
    KeyHash = keyHash;
    Role = role;
    CreatedAt = createdAt;
    CreatedByUserId = createdByUserId;
    LastUsedAt = lastUsedAt;
    RevokedAt = revokedAt;
  }

  /// <summary>Новий ключ і його відкрите значення (повернути користувачу один раз, не зберігати).</summary>
  public static (ApiKey Key, string PlainKey) Generate(string name, UserRole role, Guid? createdByUserId)
  {
    if (role == UserRole.Admin || !Enum.IsDefined(role))
      throw new DomainValidationException("API-ключ може мати роль Viewer (читання) або Editor (запис даних)");

    var plain = KeyPrefix + Base64Url(RandomNumberGenerator.GetBytes(32));
    var key = new ApiKey(
      Guid.NewGuid(), Guard.Name(name, "ключа"), plain[..PrefixLength], Hash(plain), role, DateTime.UtcNow,
      createdByUserId, lastUsedAt: null, revokedAt: null);

    return (key, plain);
  }

  public static ApiKey Restore(Guid id, string name, string prefix, byte[] keyHash, UserRole role, DateTime createdAt,
    Guid? createdByUserId, DateTime? lastUsedAt, DateTime? revokedAt)
    => new(id, name, prefix, keyHash, role, createdAt, createdByUserId, lastUsedAt, revokedAt);

  /// <summary>SHA-256 від ключа - за ним ключ шукається в БД.</summary>
  public static byte[] Hash(string plainKey) => SHA256.HashData(Encoding.UTF8.GetBytes(plainKey.Trim()));

  /// <summary>Чи схоже значення на ключ системи (швидка перевірка до звернення до БД).</summary>
  public static bool LooksValid(string? plainKey)
    => plainKey is { Length: > 20 and < 100 } && plainKey.StartsWith(KeyPrefix, StringComparison.Ordinal);

  public void Revoke(DateTime now)
  {
    if (RevokedAt is null)
      RevokedAt = now;
  }

  public void MarkUsed(DateTime now) => LastUsedAt = now;

  private static string Base64Url(byte[] bytes)
    => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
