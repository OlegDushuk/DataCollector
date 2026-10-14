namespace DataCollector.Server.Domain.Users;

/// <summary>
/// Роль користувача або API-ключа. Кожна наступна роль має всі права попередньої.
/// Значення зберігаються в БД (Users.Role, ApiKeys.Role), тому їх не можна змінювати.
/// </summary>
public enum UserRole : byte
{
  /// <summary>Перегляд: записи, зведення, дашборди, моніторинг, експорт.</summary>
  Viewer = 0,

  /// <summary>Редагування даних: записи, імпорт, дашборди, правила моніторингу.</summary>
  Editor = 1,

  /// <summary>Адміністрування: моделі даних і поля, користувачі, API-ключі.</summary>
  Admin = 2
}

public static class UserRoles
{
  public static UserRole Parse(string? value)
  {
    if (Enum.TryParse<UserRole>(value?.Trim(), ignoreCase: true, out var role) && Enum.IsDefined(role))
      return role;

    throw new Exceptions.DomainValidationException($"Невідома роль '{value}'. Доступні: Viewer, Editor, Admin");
  }

  /// <summary>Чи має роль права <paramref name="required"/> (ролі впорядковані за правами).</summary>
  public static bool Includes(this UserRole role, UserRole required) => role >= required;

  public static string Label(this UserRole role) => role switch
  {
    UserRole.Admin => "Адміністратор",
    UserRole.Editor => "Редактор",
    _ => "Перегляд"
  };
}
