using DataCollector.Server.API.Auth;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.Server.API.Controllers;

/// <summary>Користувачі (лише адміністратор). Користувачі не видаляються, а вимикаються - історія лишається.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthPolicies.CanAdmin)]
public class UsersController(IUserService userService) : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<List<UserDto>>> GetAll() => await userService.GetAll();

  [HttpPost]
  public async Task<ActionResult<CreatedIdResult>> Create([FromBody] CreateUserCommand command)
  {
    var id = await userService.Create(command);
    return StatusCode(StatusCodes.Status201Created, new CreatedIdResult { Id = id });
  }

  /// <summary>Ім'я, роль і доступ. Собі не можна зняти права адміністратора; останнього адміністратора - теж.</summary>
  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserCommand command)
  {
    await userService.Update(id, command, User.UserId() ?? Guid.Empty);
    return NoContent();
  }

  /// <summary>Новий пароль користувачу (наприклад, якщо він забув свій). Його сеанси завершуються.</summary>
  [HttpPost("{id:guid}/password")]
  public async Task<IActionResult> SetPassword(Guid id, [FromBody] SetPasswordCommand command)
  {
    await userService.SetPassword(id, command);
    return NoContent();
  }
}
