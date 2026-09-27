using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.Server.API.Controllers;

/// <summary>
/// Записи моделі. {config} - Id моделі або її ключ, тож зовнішня система може писати
/// напряму в POST api/data-configs/orders/records.
/// </summary>
[ApiController]
[Route("api/data-configs/{config}/records")]
public class DataRecordsController(
  IDataInstanceService dataInstanceService
  ) : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<PagedResult<DataInstanceDto>>> GetPage(
    string config,
    [FromQuery] GetDataInstancesQuery query)
  {
    return await dataInstanceService.GetPage(config, query);
  }

  [HttpGet("{recordId:guid}")]
  public async Task<ActionResult<DataInstanceDto>> Get(string config, Guid recordId)
  {
    return await dataInstanceService.Get(config, recordId);
  }

  [HttpPost]
  public async Task<ActionResult<CreatedIdResult>> Create(string config, [FromBody] CreateDataInstanceCommand command)
  {
    var id = await dataInstanceService.CreateInstance(config, command);
    return CreatedAtAction(nameof(Get), new { config, recordId = id }, new CreatedIdResult { Id = id });
  }

  [HttpPatch("{recordId:guid}")]
  public async Task<IActionResult> Update(string config, Guid recordId, [FromBody] UpdateDataInstanceCommand command)
  {
    await dataInstanceService.UpdateInstance(config, recordId, command);
    return NoContent();
  }

  [HttpDelete("{recordId:guid}")]
  public async Task<IActionResult> Delete(string config, Guid recordId)
  {
    await dataInstanceService.DeleteInstance(config, recordId);
    return NoContent();
  }
}
