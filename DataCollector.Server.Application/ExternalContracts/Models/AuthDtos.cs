namespace DataCollector.Server.Application.ExternalContracts.Models;

public class LoginCommand
{
  public string Login { get; set; } = string.Empty;
  public string Password { get; set; } = string.Empty;
}

/// <summary>Відповідь на вхід: токен доступу (Authorization: Bearer …), коли він закінчується, і хто увійшов.</summary>
public class LoginResultDto
{
  public string AccessToken { get; set; } = null!;
  public DateTime ExpiresAt { get; set; }
  public CurrentUserDto User { get; set; } = null!;
}

public class CurrentUserDto
{
  public Guid Id { get; set; }
  public string Login { get; set; } = null!;
  public string DisplayName { get; set; } = null!;

  /// <summary>Viewer, Editor або Admin.</summary>
  public string Role { get; set; } = null!;
}

public class ChangeOwnPasswordCommand
{
  public string CurrentPassword { get; set; } = string.Empty;
  public string NewPassword { get; set; } = string.Empty;
}

public class UserDto
{
  public Guid Id { get; set; }
  public string Login { get; set; } = null!;
  public string DisplayName { get; set; } = null!;
  public string Role { get; set; } = null!;
  public bool IsActive { get; set; }

  /// <summary>До якого часу вхід заблоковано через невдалі спроби; null - не заблоковано.</summary>
  public DateTime? LockedUntil { get; set; }

  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public DateTime? LastLoginAt { get; set; }
}

public class CreateUserCommand
{
  public string Login { get; set; } = string.Empty;
  public string? DisplayName { get; set; }
  public string Role { get; set; } = "Viewer";
  public string Password { get; set; } = string.Empty;
}

public class UpdateUserCommand
{
  public string? DisplayName { get; set; }
  public string Role { get; set; } = "Viewer";
  public bool IsActive { get; set; } = true;
}

public class SetPasswordCommand
{
  public string Password { get; set; } = string.Empty;
}

public class ApiKeyDto
{
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;

  /// <summary>Початок ключа (dc_AbCdEf…), щоб упізнати його у списку.</summary>
  public string Prefix { get; set; } = null!;

  public string Role { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public string? CreatedBy { get; set; }
  public DateTime? LastUsedAt { get; set; }
  public DateTime? RevokedAt { get; set; }
  public bool IsActive { get; set; }
}

public class CreateApiKeyCommand
{
  public string Name { get; set; } = string.Empty;

  /// <summary>Viewer (лише читання) або Editor (запис даних).</summary>
  public string Role { get; set; } = "Editor";
}

/// <summary>Новий ключ: <see cref="PlainKey"/> показується лише один раз.</summary>
public class CreatedApiKeyDto
{
  public ApiKeyDto Key { get; set; } = null!;
  public string PlainKey { get; set; } = null!;
}
