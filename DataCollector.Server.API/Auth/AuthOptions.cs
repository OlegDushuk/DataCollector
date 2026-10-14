namespace DataCollector.Server.API.Auth;

/// <summary>Налаштування входу (секція "Auth").</summary>
public sealed class AuthOptions
{
  public const string Section = "Auth";

  /// <summary>Скільки годин дійсний токен після входу.</summary>
  public int TokenLifetimeHours { get; set; } = 12;

  /// <summary>Перший адміністратор - створюється при запуску, якщо користувачів ще немає.</summary>
  public InitialAdminOptions InitialAdmin { get; set; } = new();

  public TimeSpan TokenLifetime => TimeSpan.FromHours(Math.Clamp(TokenLifetimeHours, 1, 24 * 30));

  public sealed class InitialAdminOptions
  {
    public string? Login { get; set; } = "admin";
    public string? Password { get; set; }
  }
}
