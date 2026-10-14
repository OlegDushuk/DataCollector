using System.Text.RegularExpressions;
using DataCollector.Server.Domain.Common;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Users;

/// <summary>
/// Користувач системи: вхід за логіном і паролем, роль визначає доступні дії.
/// </summary>
/// <remarks>
/// Пароль зберігається лише як хеш (PBKDF2, див. Application.Security). <see cref="SecurityStamp"/> змінюється,
/// коли змінюються пароль, роль або доступ: токени, видані до цього, перестають прийматися.
/// Після <see cref="MaxFailedLogins"/> невдалих спроб поспіль вхід блокується на <see cref="LockoutDuration"/>
/// (захист від підбору пароля).
/// </remarks>
public sealed partial class User
{
  public const int MaxLoginLength = 64;
  public const int MaxFailedLogins = 5;
  public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

  public Guid Id { get; }
  public string Login { get; }
  public string DisplayName { get; private set; }
  public UserRole Role { get; private set; }
  public string PasswordHash { get; private set; }
  public Guid SecurityStamp { get; private set; }
  public bool IsActive { get; private set; }
  public int FailedLoginCount { get; private set; }
  public DateTime? LockoutEnd { get; private set; }
  public DateTime CreatedAt { get; }
  public DateTime? UpdatedAt { get; private set; }
  public DateTime? LastLoginAt { get; private set; }

  [GeneratedRegex("^[A-Za-z0-9._@-]+$")]
  private static partial Regex LoginRegex();

  private User(
    Guid id, string login, string displayName, UserRole role, string passwordHash, Guid securityStamp,
    bool isActive, int failedLoginCount, DateTime? lockoutEnd, DateTime createdAt, DateTime? updatedAt, DateTime? lastLoginAt)
  {
    Id = id;
    Login = login;
    DisplayName = displayName;
    Role = role;
    PasswordHash = passwordHash;
    SecurityStamp = securityStamp;
    IsActive = isActive;
    FailedLoginCount = failedLoginCount;
    LockoutEnd = lockoutEnd;
    CreatedAt = createdAt;
    UpdatedAt = updatedAt;
    LastLoginAt = lastLoginAt;
  }

  /// <param name="passwordHash">Хеш уже перевіреного <see cref="PasswordPolicy"/> пароля.</param>
  public static User Create(string login, string? displayName, UserRole role, string passwordHash)
  {
    var normalizedLogin = NormalizeLogin(login);
    return new User(
      Guid.NewGuid(), normalizedLogin, NormalizeDisplayName(displayName, normalizedLogin), CheckRole(role), passwordHash,
      Guid.NewGuid(), isActive: true, failedLoginCount: 0, lockoutEnd: null, DateTime.UtcNow, updatedAt: null, lastLoginAt: null);
  }

  /// <summary>Відновлення з БД без перевірок.</summary>
  public static User Restore(
    Guid id, string login, string displayName, UserRole role, string passwordHash, Guid securityStamp,
    bool isActive, int failedLoginCount, DateTime? lockoutEnd, DateTime createdAt, DateTime? updatedAt, DateTime? lastLoginAt)
    => new(id, login, displayName, role, passwordHash, securityStamp, isActive, failedLoginCount, lockoutEnd,
      createdAt, updatedAt, lastLoginAt);

  /// <summary>Логін: 3–64 символи, латинські літери, цифри та . _ @ -. Регістр не враховується при вході.</summary>
  public static string NormalizeLogin(string? login)
  {
    var value = login?.Trim();

    if (string.IsNullOrEmpty(value) || value.Length < 3)
      throw new DomainValidationException("Логін має містити щонайменше 3 символи");

    if (value.Length > MaxLoginLength)
      throw new DomainValidationException($"Логін не може бути довшим за {MaxLoginLength} символи");

    if (!LoginRegex().IsMatch(value))
      throw new DomainValidationException("Логін може містити лише латинські літери, цифри та символи . _ @ -");

    return value;
  }

  public bool IsLockedOut(DateTime now) => LockoutEnd is { } end && end > now;

  /// <summary>Чи можна увійти зараз (без перевірки пароля).</summary>
  public bool CanSignIn(DateTime now) => IsActive && !IsLockedOut(now);

  /// <summary>Ім'я, роль і доступ (адміністратором). Зміна ролі чи доступу відкликає видані токени.</summary>
  public void Change(string? displayName, UserRole role, bool isActive)
  {
    DisplayName = NormalizeDisplayName(displayName, Login);

    if (Role != CheckRole(role) || IsActive != isActive)
      SecurityStamp = Guid.NewGuid();

    Role = role;
    IsActive = isActive;

    // Повернути доступ - означає й зняти блокування за невдалі спроби.
    if (isActive)
      Unlock();

    UpdatedAt = DateTime.UtcNow;
  }

  /// <summary>Новий пароль: відкликає видані токени і знімає блокування.</summary>
  public void SetPassword(string passwordHash)
  {
    PasswordHash = passwordHash;
    SecurityStamp = Guid.NewGuid();
    Unlock();
    UpdatedAt = DateTime.UtcNow;
  }

  /// <summary>Невдала спроба входу. Повертає true, якщо після неї вхід заблоковано.</summary>
  public bool RegisterFailedLogin(DateTime now)
  {
    FailedLoginCount++;

    if (FailedLoginCount < MaxFailedLogins)
      return false;

    FailedLoginCount = 0;
    LockoutEnd = now + LockoutDuration;
    return true;
  }

  public void RegisterLogin(DateTime now)
  {
    Unlock();
    LastLoginAt = now;
  }

  private void Unlock()
  {
    FailedLoginCount = 0;
    LockoutEnd = null;
  }

  private static string NormalizeDisplayName(string? displayName, string login)
    => string.IsNullOrWhiteSpace(displayName) ? login : Guard.Name(displayName, "користувача");

  private static UserRole CheckRole(UserRole role)
    => Enum.IsDefined(role) ? role : throw new DomainValidationException($"Невідома роль {(int)role}");
}

/// <summary>Вимоги до пароля: 8–128 символів, хоча б одна літера і одна цифра.</summary>
public static class PasswordPolicy
{
  public const int MinLength = 8;
  public const int MaxLength = 128;

  public const string Description = "щонайменше 8 символів, з літерами й цифрами";

  public static void Validate(string? password)
  {
    if (string.IsNullOrEmpty(password) || password.Length < MinLength)
      throw new DomainValidationException($"Пароль має містити щонайменше {MinLength} символів");

    if (password.Length > MaxLength)
      throw new DomainValidationException($"Пароль не може бути довшим за {MaxLength} символів");

    if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
      throw new DomainValidationException("Пароль має містити і літери, і цифри");
  }
}
