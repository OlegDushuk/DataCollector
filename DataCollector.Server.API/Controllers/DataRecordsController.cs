using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.API.Auth;
using DataCollector.Server.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
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
    [FromQuery] GetDataInstancesQuery query,
    CancellationToken cancellationToken)
  {
    // cancellationToken = HttpContext.RequestAborted: спрацьовує, коли клієнт розірвав запит
    // (наприклад, UI перейшов на іншу сторінку таблиці, не дочекавшись цієї).
    return await dataInstanceService.GetPage(config, query, cancellationToken);
  }

  /// <summary>
  /// Пошук записів з умовами фільтра. POST, бо умови - складна структура, яку незручно передавати в адресі.
  /// </summary>
  [HttpPost("query")]
  public async Task<ActionResult<PagedResult<DataInstanceDto>>> Query(
    string config,
    [FromBody] QueryDataInstancesCommand command,
    CancellationToken cancellationToken)
  {
    return await dataInstanceService.Query(config, command, cancellationToken);
  }

  /// <summary>
  /// Групування записів і показники по групах (кількість, сума, середнє, мінімум, максимум).
  /// Дані для діаграм, звітів і моніторингу.
  /// </summary>
  [HttpPost("aggregate")]
  public async Task<ActionResult<AggregateResultDto>> Aggregate(
    string config,
    [FromBody] AggregateDataInstancesCommand command,
    CancellationToken cancellationToken)
  {
    return await dataInstanceService.Aggregate(config, command, cancellationToken);
  }

  /// <summary>Запис; його версія - також у заголовку ETag.</summary>
  [HttpGet("{recordId:guid}")]
  public async Task<ActionResult<DataInstanceDto>> Get(string config, Guid recordId)
  {
    var record = await dataInstanceService.Get(config, recordId);
    ETags.Set(Response, record.Version);
    return record;
  }

  [Authorize(Policy = AuthPolicies.CanEdit)]
  [HttpPost]
  public async Task<ActionResult<CreatedIdResult>> Create(string config, [FromBody] CreateDataInstanceCommand command)
  {
    var id = await dataInstanceService.CreateInstance(config, command);
    return CreatedAtAction(nameof(Get), new { config, recordId = id }, new CreatedIdResult { Id = id });
  }

  /// <summary>
  /// Змінює передані поля. Версія, яку бачив клієнт, - у полі version або в заголовку If-Match:
  /// якщо запис відтоді змінили, відповідь 409. Нова версія - у заголовку ETag відповіді.
  /// </summary>
  [Authorize(Policy = AuthPolicies.CanEdit)]
  [HttpPatch("{recordId:guid}")]
  public async Task<IActionResult> Update(string config, Guid recordId, [FromBody] UpdateDataInstanceCommand command)
  {
    command.Version ??= ETags.IfMatch(Request);

    var version = await dataInstanceService.UpdateInstance(config, recordId, command);
    ETags.Set(Response, version);
    return NoContent();
  }

  /// <summary>Видаляє запис; з заголовком If-Match - лише якщо версія запису не змінилась (інакше 409).</summary>
  [Authorize(Policy = AuthPolicies.CanEdit)]
  [HttpDelete("{recordId:guid}")]
  public async Task<IActionResult> Delete(string config, Guid recordId)
  {
    await dataInstanceService.DeleteInstance(config, recordId, ETags.IfMatch(Request));
    return NoContent();
  }
}
