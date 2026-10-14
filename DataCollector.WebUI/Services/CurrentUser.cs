using System.Security.Claims;
using DataCollector.WebUI.Auth;
using DataCollector.WebUI.Entities;
using Microsoft.AspNetCore.Components.Authorization;

namespace DataCollector.WebUI.Services;

/// <summary>
/// Хто працює з інтерфейсом у цьому вікні: ім'я, роль, що йому дозволено, і токен для запитів до API.
/// </summary>
/// <remarks>
/// Користувач визначається з cookie сеансу на початку з'єднання Blazor і не змінюється до перезавантаження
/// сторінки, тож стан автентифікації вже готовий і читається синхронно (для умов у розмітці).
/// </remarks>
public sealed class CurrentUser(AuthenticationStateProvider authenticationState)
{
  private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

  public ClaimsPrincipal Principal
    => authenticationState.GetAuthenticationStateAsync() is { IsCompletedSuccessfully: true } state
      ? state.Result.User
      : Anonymous;

  public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

  public Guid? Id => Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
  public string Login => Principal.Identity?.Name ?? string.Empty;
  public string DisplayName => Principal.FindFirstValue(UserClaims.DisplayName) ?? Login;
  public string Role => Principal.FindFirstValue(ClaimTypes.Role) ?? UserRoles.Viewer;
  public string RoleLabel => UserRoles.Label(Role);

  public bool CanEdit => IsAuthenticated && UserRoles.Includes(Role, UserRoles.Editor);
  public bool CanAdmin => IsAuthenticated && UserRoles.Includes(Role, UserRoles.Admin);

  public async Task<string?> AccessToken()
  {
    var state = await authenticationState.GetAuthenticationStateAsync();
    return state.User.FindFirstValue(UserClaims.AccessToken);
  }
}
