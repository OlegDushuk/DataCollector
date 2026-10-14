using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Application.Security;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DataCollector.Server.Application.Services;

public class AuthService(
  IUserRepository users,
  IPasswordHasher passwordHasher,
  IMemoryCache cache,
  ILogger<AuthService> logger
) : IAuthService
{
  private const string InvalidCredentials = "Невірний логін або пароль";

  /// <summary>Скільки секунд тримати в пам'яті стан користувача для перевірки токенів.</summary>
  private static readonly TimeSpan SessionCacheTime = TimeSpan.FromSeconds(30);

  /// <summary>
  /// Хеш, з яким порівнюється пароль неіснуючого користувача: відповідь займає стільки ж часу,
  /// тож за часом не видно, чи існує такий логін.
  /// </summary>
  private static string? _dummyHash;

  public async Task<AuthenticatedUser> Login(LoginCommand command)
  {
    var now = DateTime.UtcNow;
    var login = command.Login?.Trim() ?? string.Empty;
    var user = IsLoginFormat(login) ? await users.GetByLogin(login) : null;

    if (user is null)
    {
      passwordHasher.Verify(command.Password ?? string.Empty, _dummyHash ??= passwordHasher.Hash("not-a-password-1"));
      logger.LogWarning("Невдалий вхід: користувача {Login} не існує", login);
      throw new AuthenticationFailedException(InvalidCredentials);
    }

    if (user.IsLockedOut(now))
    {
      logger.LogWarning("Вхід відхилено: користувач {Login} заблокований до {LockoutEnd:u}", user.Login, user.LockoutEnd);
      throw new AuthenticationFailedException(LockedMessage(user, now));
    }

    if (!passwordHasher.Verify(command.Password ?? string.Empty, user.PasswordHash))
    {
      var locked = user.RegisterFailedLogin(now);
      await users.Update(user);

      if (locked)
      {
        logger.LogWarning("Невдалий вхід: невірний пароль {Login}; вхід заблоковано на {Minutes} хв після {Attempts} спроб",
          user.Login, User.LockoutDuration.TotalMinutes, User.MaxFailedLogins);
        throw new AuthenticationFailedException(LockedMessage(user, now));
      }

      logger.LogWarning("Невдалий вхід: невірний пароль {Login} (спроба {Attempt} з {Max})",
        user.Login, user.FailedLoginCount, User.MaxFailedLogins);
      throw new AuthenticationFailedException(InvalidCredentials);
    }

    // Пароль правильний - лише тоді кажемо, що доступ вимкнено (інакше це підказка, що логін існує).
    if (!user.IsActive)
    {
      logger.LogWarning("Вхід відхилено: обліковий запис {Login} вимкнено", user.Login);
      throw new AuthenticationFailedException("Обліковий запис вимкнено. Зверніться до адміністратора.");
    }

    user.RegisterLogin(now);
    await users.Update(user);

    logger.LogInformation("Користувач {Login} увійшов (роль {Role})", user.Login, user.Role);
    return new AuthenticatedUser(user.Id, user.Login, user.DisplayName, user.Role, user.SecurityStamp);
  }

  public async Task<bool> IsSessionValid(Guid userId, Guid securityStamp)
  {
    var state = await cache.GetOrCreateAsync(CacheKey(userId), async entry =>
    {
      entry.AbsoluteExpirationRelativeToNow = SessionCacheTime;
      var user = await users.GetById(userId);
      return user is null ? null : new SessionState(user.SecurityStamp, user.IsActive);
    });

    return state is { IsActive: true } && state.SecurityStamp == securityStamp;
  }

  public async Task<CurrentUserDto> GetCurrent(Guid userId)
  {
    var user = await users.GetById(userId) ?? throw new NotFoundException("Користувач не знайдений");
    return user.ToCurrentDto();
  }

  public async Task ChangeOwnPassword(Guid userId, ChangeOwnPasswordCommand command)
  {
    var user = await users.GetById(userId) ?? throw new NotFoundException("Користувач не знайдений");

    if (!passwordHasher.Verify(command.CurrentPassword ?? string.Empty, user.PasswordHash))
      throw new DomainValidationException("Поточний пароль введено невірно");

    PasswordPolicy.Validate(command.NewPassword);
    user.SetPassword(passwordHasher.Hash(command.NewPassword));
    await users.Update(user);
    InvalidateSession(cache, user.Id);

    logger.LogInformation("Користувач {Login} змінив свій пароль", user.Login);
  }

  public async Task<bool> EnsureInitialAdmin(string? login, string? password)
  {
    if ((await users.GetAll()).Count > 0)
      return false;

    if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
    {
      logger.LogWarning(
        "Користувачів ще немає. Задайте Auth:InitialAdmin:Login і Auth:InitialAdmin:Password, щоб створити першого адміністратора");
      return false;
    }

    PasswordPolicy.Validate(password);
    var admin = User.Create(login, "Адміністратор", UserRole.Admin, passwordHasher.Hash(password));
    await users.Create(admin);

    logger.LogWarning("Створено першого адміністратора {Login} з налаштувань - змініть пароль після входу",
      admin.Login);
    return true;
  }

  internal static void InvalidateSession(IMemoryCache cache, Guid userId) => cache.Remove(CacheKey(userId));

  private static string CacheKey(Guid userId) => $"auth:user:{userId:N}";

  private static bool IsLoginFormat(string login)
  {
    try
    {
      User.NormalizeLogin(login);
      return true;
    }
    catch (DomainValidationException)
    {
      return false;
    }
  }

  private static string LockedMessage(User user, DateTime now)
  {
    var minutes = Math.Max(1, (int)Math.Ceiling(((user.LockoutEnd ?? now) - now).TotalMinutes));
    return $"Забагато невдалих спроб входу. Спробуйте знову через {minutes} хв.";
  }

  private sealed record SessionState(Guid SecurityStamp, bool IsActive);
}

internal static class UserMapping
{
  public static CurrentUserDto ToCurrentDto(this User user) => new()
  {
    Id = user.Id,
    Login = user.Login,
    DisplayName = user.DisplayName,
    Role = user.Role.ToString()
  };

  public static UserDto ToDto(this User user, DateTime now) => new()
  {
    Id = user.Id,
    Login = user.Login,
    DisplayName = user.DisplayName,
    Role = user.Role.ToString(),
    IsActive = user.IsActive,
    LockedUntil = user.IsLockedOut(now) ? user.LockoutEnd : null,
    CreatedAt = user.CreatedAt,
    UpdatedAt = user.UpdatedAt,
    LastLoginAt = user.LastLoginAt
  };

  public static ApiKeyDto ToDto(this ApiKey key, string? createdBy) => new()
  {
    Id = key.Id,
    Name = key.Name,
    Prefix = key.Prefix,
    Role = key.Role.ToString(),
    CreatedAt = key.CreatedAt,
    CreatedBy = createdBy,
    LastUsedAt = key.LastUsedAt,
    RevokedAt = key.RevokedAt,
    IsActive = key.IsActive
  };
}
