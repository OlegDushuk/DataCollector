using System.Security.Claims;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Domain.Users;

namespace DataCollector.Server.API.Auth;

/// <summary>Схеми автентифікації API.</summary>
public static class AuthSchemes
{
  /// <summary>Вибирає схему за запитом: є заголовок X-Api-Key - ключ, інакше - токен користувача.</summary>
  public const string Default = "DataCollector";

  /// <summary>Токен користувача (Authorization: Bearer …), виданий POST api/auth/login.</summary>
  public const string Bearer = "Bearer";

  /// <summary>API-ключ зовнішньої системи (X-Api-Key: dc_…).</summary>
  public const string ApiKey = "ApiKey";

  public const string ApiKeyHeader = "X-Api-Key";
}

/// <summary>
/// Політики доступу. Без атрибута дію може виконати будь-хто, хто пройшов автентифікацію (перегляд).
/// </summary>
public static class AuthPolicies
{
  /// <summary>Зміна даних: записи, імпорт, дашборди, правила моніторингу.</summary>
  public const string CanEdit = "CanEdit";

  /// <summary>Адміністрування: моделі й поля, користувачі, API-ключі.</summary>
  public const string CanAdmin = "CanAdmin";
}

/// <summary>Власні типи claims у токені та ідентичності запиту.</summary>
public static class AuthClaims
{
  public const string SecurityStamp = "dc:stamp";
  public const string DisplayName = "dc:display_name";

  /// <summary>Хто виконує запит: "user" або "api_key".</summary>
  public const string Kind = "dc:kind";

  public const string KindUser = "user";
  public const string KindApiKey = "api_key";

  public static ClaimsPrincipal ForUser(AuthenticatedUser user) => new(new ClaimsIdentity(
  [
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new Claim(ClaimTypes.Name, user.Login),
    new Claim(DisplayName, user.DisplayName),
    new Claim(ClaimTypes.Role, user.Role.ToString()),
    new Claim(SecurityStamp, user.SecurityStamp.ToString()),
    new Claim(Kind, KindUser)
  ], AuthSchemes.Bearer));

  public static ClaimsPrincipal ForApiKey(ApiKeyIdentity key) => new(new ClaimsIdentity(
  [
    new Claim(ClaimTypes.NameIdentifier, key.Id.ToString()),
    new Claim(ClaimTypes.Name, "api-key:" + key.Name),
    new Claim(DisplayName, key.Name),
    new Claim(ClaimTypes.Role, key.Role.ToString()),
    new Claim(Kind, KindApiKey)
  ], AuthSchemes.ApiKey));

  /// <summary>Ролі, яким дозволено дію з мінімальною роллю <paramref name="role"/> (ролі впорядковані за правами).</summary>
  public static string[] RolesFrom(UserRole role)
    => Enum.GetValues<UserRole>().Where(r => r.Includes(role)).Select(r => r.ToString()).ToArray();
}

public static class ClaimsPrincipalExtensions
{
  /// <summary>Id користувача, якщо запит від користувача (не від API-ключа).</summary>
  public static Guid? UserId(this ClaimsPrincipal principal)
    => principal.FindFirstValue(AuthClaims.Kind) == AuthClaims.KindUser &&
       Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
      ? id
      : null;
}
