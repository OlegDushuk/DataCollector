using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.Server.API.Controllers;

[ApiController]
[Route("api/data-config")]
public class DataConfigController(
  IDataConfigService dataConfigService
  ) : ControllerBase
{
  [HttpPost]
  public async Task<IActionResult> Create([FromBody] CreateEntityConfigCommand command)
  {
    try
    {
      await dataConfigService.CreateDataConfig(command);
      return Ok();
    }
    catch (Exception e)
    {
      Console.WriteLine("---");
      Console.WriteLine(e);
      Console.WriteLine("---");
      return StatusCode(500);
    }
  }

  [HttpPatch]
  public async Task<IActionResult> Edit([FromBody] EditEntityConfigCommand command)
  {
    try
    {
      await dataConfigService.EditDataConfig(command);
      return Ok();
    }
    catch (Exception e)
    {
      Console.WriteLine("---");
      Console.WriteLine(e);
      Console.WriteLine("---");
      return StatusCode(500);
    }
  }
}