using System.Security.Claims;
using DataCollector.Server.Application.ExternalContracts.Interfaces;

namespace DataCollector.Server.API.Auth;

/// <summary>
/// Перевіряє, що токен користувача ще дійсний: користувач не вимкнений, а пароль, роль і доступ не змінювались
/// після видачі токена (позначка безпеки в токені збігається з поточною). Інакше запит вважається анонімним
/// і отримує 401 - так вимкнення користувача або зміна пароля діють одразу, а не після закінчення токена.
/// </summary>
/// <remarks>
/// Також додає до всіх записів журналу в межах запиту, хто його виконує (UserName, AuthKind).
/// </remarks>
public sealed class SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger)
{
  public async Task InvokeAsync(HttpContext context, IAuthService auth)
  {
    var user = context.User;

    if (user.Identity?.IsAuthenticated == true &&
        user.FindFirstValue(AuthClaims.Kind) == AuthClaims.KindUser &&
        user.UserId() is { } userId)
    {
      var valid = Guid.TryParse(user.FindFirstValue(AuthClaims.SecurityStamp), out var stamp) &&
                  await auth.IsSessionValid(userId, stamp);

      if (!valid)
      {
        logger.LogInformation("Токен користувача {Login} більше не дійсний (змінено пароль, роль або доступ)",
          user.Identity.Name);
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
      }
    }

    var scope = new Dictionary<string, object?>
    {
      ["UserName"] = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : "anonymous",
      ["AuthKind"] = context.User.FindFirstValue(AuthClaims.Kind) ?? "none"
    };

    using (logger.BeginScope(scope))
      await next(context);
  }
}
