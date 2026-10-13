using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
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
    return await dashboardService.Get(id);
  }

  [HttpPost]
  public async Task<ActionResult<CreatedIdResult>> Create([FromBody] SaveDashboardCommand command)
  {
    var id = await dashboardService.Create(command);
    return CreatedAtAction(nameof(Get), new { id }, new CreatedIdResult { Id = id });
  }

  /// <summary>Повне збереження: назва, опис, автооновлення і весь набір віджетів.</summary>
  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Save(Guid id, [FromBody] SaveDashboardCommand command)
  {
    await dashboardService.Save(id, command);
    return NoContent();
  }

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(Guid id)
  {
    await dashboardService.Delete(id);
    return NoContent();
  }

  [HttpPost("{id:guid}/copy")]
  public async Task<ActionResult<CreatedIdResult>> Copy(Guid id)
  {
    var copyId = await dashboardService.Copy(id);
    return CreatedAtAction(nameof(Get), new { id = copyId }, new CreatedIdResult { Id = copyId });
  }
}
