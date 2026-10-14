using DataCollector.Server.Domain.Users;

namespace DataCollector.Server.Application.Interfaces.Repositories;

public interface IUserRepository
{
  Task<List<User>> GetAll();
  Task<User?> GetById(Guid id);

  /// <summary>Пошук без урахування регістру логіна.</summary>
  Task<User?> GetByLogin(string login);

  Task Create(User user);

  /// <summary>Зберігає всі змінювані поля (ім'я, роль, пароль, доступ, стан входу).</summary>
  Task Update(User user);
}

/// <summary>Ключ і ім'я користувача, який його створив (для списку).</summary>
public sealed record ApiKeyRecord(ApiKey Key, string? CreatedByName);

public interface IApiKeyRepository
{
  Task<List<ApiKeyRecord>> GetAll();
  Task<ApiKey?> GetById(Guid id);
  Task<ApiKey?> GetByHash(byte[] keyHash);
  Task Create(ApiKey key);
  Task Revoke(Guid id, DateTime revokedAt);
  Task MarkUsed(Guid id, DateTime usedAt);
}
