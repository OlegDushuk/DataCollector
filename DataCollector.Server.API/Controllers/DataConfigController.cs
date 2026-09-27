using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.Server.API.Controllers;

/// <summary>
/// Моделі даних та їх поля. {config} - Id моделі або її ключ.
/// </summary>
[ApiController]
[Route("api/data-configs")]
public class DataConfigController(
  IDataConfigService dataConfigService
  ) : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<List<EntityConfigListItemDto>>> GetAll()
  {
    return await dataConfigService.GetAll();
  }

  [HttpGet("{config}")]
  public async Task<ActionResult<EntityConfigDto>> Get(string config)
  {
    return await dataConfigService.Get(config);
  }

  [HttpPost]
  public async Task<ActionResult<CreatedIdResult>> Create([FromBody] CreateEntityConfigCommand command)
  {
    var id = await dataConfigService.CreateDataConfig(command);
    return CreatedAtAction(nameof(Get), new { config = id }, new CreatedIdResult { Id = id });
  }

  [HttpPatch("{config}")]
  public async Task<IActionResult> Edit(string config, [FromBody] EditEntityConfigCommand command)
  {
    await dataConfigService.EditDataConfig(config, command);
    return NoContent();
  }

  [HttpDelete("{config}")]
  public async Task<IActionResult> Delete(string config)
  {
    await dataConfigService.DeleteDataConfig(config);
    return NoContent();
  }

  [HttpPost("{config}/properties")]
  public async Task<ActionResult<List<Guid>>> AddProperties(string config, [FromBody] AddPropertiesCommand command)
  {
    return await dataConfigService.AddProperties(config, command);
  }

  [HttpPatch("{config}/properties/{propertyId:guid}")]
  public async Task<IActionResult> EditProperty(string config, Guid propertyId, [FromBody] EditPropertyCommand command)
  {
    await dataConfigService.EditProperty(config, propertyId, command);
    return NoContent();
  }

  [HttpDelete("{config}/properties/{propertyId:guid}")]
  public async Task<IActionResult> DeleteProperty(string config, Guid propertyId)
  {
    await dataConfigService.DeleteProperty(config, propertyId);
    return NoContent();
  }
}
