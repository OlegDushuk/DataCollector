using DataCollector.Server.API.Auth;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.Server.API.Controllers;

/// <summary>API-ключі зовнішніх систем і пристроїв (лише адміністратор).</summary>
[ApiController]
[Route("api/api-keys")]
[Authorize(Policy = AuthPolicies.CanAdmin)]
public class ApiKeysController(IApiKeyService apiKeyService) : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<List<ApiKeyDto>>> GetAll() => await apiKeyService.GetAll();

  /// <summary>Новий ключ. Значення ключа є лише в цій відповіді - система зберігає тільки його хеш.</summary>
  [HttpPost]
  public async Task<ActionResult<CreatedApiKeyDto>> Create([FromBody] CreateApiKeyCommand command)
  {
    var created = await apiKeyService.Create(command, User.UserId());
    return StatusCode(StatusCodes.Status201Created, created);
  }

  /// <summary>Відкликати ключ: запити з ним одразу отримують 401.</summary>
  [HttpPost("{id:guid}/revoke")]
  public async Task<IActionResult> Revoke(Guid id)
  {
    await apiKeyService.Revoke(id);
    return NoContent();
  }
}
