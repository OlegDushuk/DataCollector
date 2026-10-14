using DataCollector.Server.API.Auth;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DataCollector.Server.API.Controllers;

/// <summary>
/// Вхід і власний обліковий запис. Токен з відповіді на вхід передається в заголовку
/// <c>Authorization: Bearer …</c>; зовнішні системи замість цього використовують <c>X-Api-Key</c>.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(
  IAuthService authService,
  IOptionsMonitor<BearerTokenOptions> bearerOptions,
  IOptions<AuthOptions> authOptions
) : ControllerBase
{
  /// <summary>Логін і пароль -> токен доступу. Після 5 невдалих спроб вхід блокується на 5 хвилин.</summary>
  [AllowAnonymous]
  [HttpPost("login")]
  public async Task<ActionResult<LoginResultDto>> Login([FromBody] LoginCommand command)
  {
    var user = await authService.Login(command);

    var issued = DateTimeOffset.UtcNow;
    var expires = issued + authOptions.Value.TokenLifetime;

    // Токен - зашифрований і підписаний захистом даних ASP.NET Core квиток із claims користувача;
    // його перевіряє стандартний обробник BearerToken. ExpiresUtc обов'язковий: без нього токен був би безстроковим.
    var ticket = new AuthenticationTicket(
      AuthClaims.ForUser(user),
      new AuthenticationProperties { IssuedUtc = issued, ExpiresUtc = expires },
      AuthSchemes.Bearer);

    var token = bearerOptions.Get(AuthSchemes.Bearer).BearerTokenProtector.Protect(ticket);

    return new LoginResultDto
    {
      AccessToken = token,
      ExpiresAt = expires.UtcDateTime,
      User = new CurrentUserDto
      {
        Id = user.Id,
        Login = user.Login,
        DisplayName = user.DisplayName,
        Role = user.Role.ToString()
      }
    };
  }

  /// <summary>Хто виконує запит (для API-ключа - назва і роль ключа).</summary>
  [HttpGet("me")]
  public async Task<ActionResult<CurrentUserDto>> Me()
  {
    if (User.UserId() is { } userId)
      return await authService.GetCurrent(userId);

    return new CurrentUserDto
    {
      Id = Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty,
      Login = User.Identity?.Name ?? string.Empty,
      DisplayName = User.FindFirst(AuthClaims.DisplayName)?.Value ?? string.Empty,
      Role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty
    };
  }

  /// <summary>Змінити власний пароль (потрібен поточний). Інші сеанси цього користувача завершуються.</summary>
  [HttpPost("change-password")]
  public async Task<IActionResult> ChangePassword([FromBody] ChangeOwnPasswordCommand command)
  {
    if (User.UserId() is not { } userId)
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Некоректний запит",
        detail: "Пароль змінює лише користувач, а не API-ключ");

    await authService.ChangeOwnPassword(userId, command);
    return NoContent();
  }
}
