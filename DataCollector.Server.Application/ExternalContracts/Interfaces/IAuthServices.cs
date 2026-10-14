using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Domain.Users;

namespace DataCollector.Server.Application.ExternalContracts.Interfaces;

/// <summary>Користувач, що успішно увійшов: з цього хост API будує токен.</summary>
public sealed record AuthenticatedUser(Guid Id, string Login, string DisplayName, UserRole Role, Guid SecurityStamp);

/// <summary>Власник API-ключа: з цього хост API будує ідентичність запиту.</summary>
public sealed record ApiKeyIdentity(Guid Id, string Name, UserRole Role);

/// <summary>Вхід, перевірка сеансу і власний пароль.</summary>
public interface IAuthService
{
  /// <summary>Перевіряє логін і пароль; невдача - AuthenticationFailedException (з урахуванням блокування).</summary>
  Task<AuthenticatedUser> Login(LoginCommand command);

  /// <summary>
  /// Чи дійсний ще токен, виданий з <paramref name="securityStamp"/>: користувач існує, активний,
  /// і з того часу не змінювались пароль, роль чи доступ. Результат кешується на кілька секунд.
  /// </summary>
  Task<bool> IsSessionValid(Guid userId, Guid securityStamp);

  Task<CurrentUserDto> GetCurrent(Guid userId);

  Task ChangeOwnPassword(Guid userId, ChangeOwnPasswordCommand command);

  /// <summary>Якщо користувачів ще немає - створює першого адміністратора (налаштування Auth:InitialAdmin).</summary>
  /// <returns>true, якщо адміністратора створено.</returns>
  Task<bool> EnsureInitialAdmin(string? login, string? password);
}

/// <summary>Керування користувачами (адміністратор).</summary>
public interface IUserService
{
  Task<List<UserDto>> GetAll();
  Task<Guid> Create(CreateUserCommand command);

  /// <param name="actingUserId">Хто змінює: собі не можна зняти права адміністратора або вимкнути доступ.</param>
  Task Update(Guid id, UpdateUserCommand command, Guid actingUserId);

  Task SetPassword(Guid id, SetPasswordCommand command);
}

/// <summary>API-ключі зовнішніх систем.</summary>
public interface IApiKeyService
{
  Task<List<ApiKeyDto>> GetAll();
  Task<CreatedApiKeyDto> Create(CreateApiKeyCommand command, Guid? createdByUserId);
  Task Revoke(Guid id);

  /// <summary>Чий це ключ; null - ключ невідомий або відкликаний. Результат кешується на хвилину.</summary>
  Task<ApiKeyIdentity?> Authenticate(string plainKey);
}
