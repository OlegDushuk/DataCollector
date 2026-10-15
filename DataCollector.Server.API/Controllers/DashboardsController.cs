using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.API.Auth;
using DataCollector.Server.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.Server.API.Controllers;

/// <summary>
/// Дашборди. Дані віджетів тут не віддаються - інтерфейс отримує їх через ендпоінти записів
/// (<c>records/query</c>, <c>records/aggregate</c>) з налаштуваннями віджета.
/// </summary>
[ApiController]
[Route("api/dashboards")]
public class DashboardsController(IDashboardService dashboardService) : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<List<DashboardListItemDto>>> GetAll()
  {
    return await dashboardService.GetAll();
  }

  [HttpGet("{id:guid}")]
  public async Task<ActionResult<DashboardDto>> Get(Guid id)
  {
    var dashboard = await dashboardService.Get(id);
    ETags.Set(Response, dashboard.Version);
    return dashboard;
  }

  [Authorize(Policy = AuthPolicies.CanEdit)]
  [HttpPost]
  public async Task<ActionResult<CreatedIdResult>> Create([FromBody] SaveDashboardCommand command)
  {
    var id = await dashboardService.Create(command);
    return CreatedAtAction(nameof(Get), new { id }, new CreatedIdResult { Id = id });
  }

  /// <summary>
  /// Повне збереження: назва, опис, автооновлення і весь набір віджетів. Версія, яку бачив редактор, -
  /// у полі version або в заголовку If-Match (змінено відтоді - 409). Нова версія - у заголовку ETag.
  /// </summary>
  [Authorize(Policy = AuthPolicies.CanEdit)]
  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Save(Guid id, [FromBody] SaveDashboardCommand command)
  {
    command.Version ??= ETags.IfMatch(Request);

    var version = await dashboardService.Save(id, command);
    ETags.Set(Response, version);
    return NoContent();
  }

  [Authorize(Policy = AuthPolicies.CanEdit)]
  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(Guid id)
  {
    await dashboardService.Delete(id);
    return NoContent();
  }

  [Authorize(Policy = AuthPolicies.CanEdit)]
  [HttpPost("{id:guid}/copy")]
  public async Task<ActionResult<CreatedIdResult>> Copy(Guid id)
  {
    var copyId = await dashboardService.Copy(id);
    return CreatedAtAction(nameof(Get), new { id = copyId }, new CreatedIdResult { Id = copyId });
  }
}
