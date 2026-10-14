using System.Security.Claims;
using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.Auth;

/// <summary>Ті самі політики, що й в API: інтерфейс ховає дії, які API однаково не дозволить.</summary>
public static class AuthPolicies
{
  /// <summary>Зміна даних: записи, імпорт, дашборди, правила моніторингу (редактор і адміністратор).</summary>
  public const string CanEdit = "CanEdit";

  /// <summary>Моделі й поля, користувачі, API-ключі (адміністратор).</summary>
  public const string CanAdmin = "CanAdmin";
}

/// <summary>Що зберігається в cookie сеансу інтерфейсу (зашифрованій захистом даних ASP.NET Core).</summary>
public static class UserClaims
{
  public const string DisplayName = "dc:display_name";

  /// <summary>Токен доступу до API - з ним інтерфейс робить запити від імені користувача.</summary>
  public const string AccessToken = "dc:access_token";

  public static ClaimsPrincipal Create(LoginResult result) => new(new ClaimsIdentity(
  [
    new Claim(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
    new Claim(ClaimTypes.Name, result.User.Login),
    new Claim(DisplayName, result.User.DisplayName),
    new Claim(ClaimTypes.Role, result.User.Role),
    new Claim(AccessToken, result.AccessToken)
  ], Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme));
}
