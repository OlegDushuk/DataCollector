namespace DataCollector.WebUI.Entities;

public class LoginResult
{
  public string AccessToken { get; set; } = null!;
  public DateTime ExpiresAt { get; set; }
  public CurrentUserInfo User { get; set; } = null!;
}

public class CurrentUserInfo
{
  public Guid Id { get; set; }
  public string Login { get; set; } = null!;
  public string DisplayName { get; set; } = null!;
  public string Role { get; set; } = null!;
}

public class UserItem
{
  public Guid Id { get; set; }
  public string Login { get; set; } = null!;
  public string DisplayName { get; set; } = null!;
  public string Role { get; set; } = null!;
  public bool IsActive { get; set; }
  public DateTime? LockedUntil { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public DateTime? LastLoginAt { get; set; }
}

public class ApiKeyItem
{
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;
  public string Prefix { get; set; } = null!;
  public string Role { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public string? CreatedBy { get; set; }
  public DateTime? LastUsedAt { get; set; }
  public DateTime? RevokedAt { get; set; }
  public bool IsActive { get; set; }
}

public class CreatedApiKey
{
  public ApiKeyItem Key { get; set; } = null!;
  public string PlainKey { get; set; } = null!;
}

/// <summary>Ролі (як в API): кожна наступна має всі права попередньої.</summary>
public static class UserRoles
{
  public const string Viewer = "Viewer";
  public const string Editor = "Editor";
  public const string Admin = "Admin";

  public static readonly IReadOnlyList<(string Code, string Label, string Hint)> All =
  [
    (Viewer, "Перегляд", "записи, зведення, дашборди, моніторинг, експорт"),
    (Editor, "Редактор", "+ зміна записів, імпорт, дашборди, правила моніторингу"),
    (Admin, "Адміністратор", "+ моделі й поля, користувачі, API-ключі")
  ];

  /// <summary>Ролі API-ключа: адміністрування ключем недоступне.</summary>
  public static readonly IReadOnlyList<(string Code, string Label, string Hint)> ForApiKey =
  [
    (Viewer, "Читання", "лише отримання даних і експорт"),
    (Editor, "Запис даних", "створення, зміна й імпорт записів, читання")
  ];

  public static string Label(string? role) => All.FirstOrDefault(r => r.Code == role).Label ?? role ?? "—";

  public static bool Includes(string? role, string required) => Rank(role) >= Rank(required);

  private static int Rank(string? role) => role switch
  {
    Admin => 2,
    Editor => 1,
    _ => 0
  };
}

/// <summary>Форма користувача (створення і зміна).</summary>
public class UserForm
{
  public Guid? Id { get; set; }
  public string Login { get; set; } = string.Empty;
  public string DisplayName { get; set; } = string.Empty;
  public string Role { get; set; } = UserRoles.Viewer;
  public bool IsActive { get; set; } = true;
  public string Password { get; set; } = string.Empty;

  public bool IsNew => Id is null;

  public static UserForm From(UserItem user) => new()
  {
    Id = user.Id,
    Login = user.Login,
    DisplayName = user.DisplayName,
    Role = user.Role,
    IsActive = user.IsActive
  };
}
