using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.Server.API.Controllers;

/// <summary>
/// Моніторинг: правила (показник за вікно часу, порівняний з порогом) і сповіщення про зміну їхнього стану.
/// Перевіряє правила фоновий сервіс (MonitoringWorker); тут - керування і перегляд.
/// </summary>
[ApiController]
[Route("api/monitoring")]
public class MonitoringController(IMonitoringService monitoring) : ControllerBase
{
  [HttpGet("rules")]
  public async Task<ActionResult<List<MonitoringRuleDto>>> GetRules()
    => await monitoring.GetRules();

  [HttpGet("rules/{id:guid}")]
  public async Task<ActionResult<MonitoringRuleDto>> GetRule(Guid id)
    => await monitoring.GetRule(id);

  [HttpPost("rules")]
  public async Task<ActionResult<CreatedIdResult>> CreateRule([FromBody] SaveMonitoringRuleCommand command)
  {
    var id = await monitoring.CreateRule(command);
    return CreatedAtAction(nameof(GetRule), new { id }, new CreatedIdResult { Id = id });
  }

  [HttpPut("rules/{id:guid}")]
  public async Task<IActionResult> UpdateRule(Guid id, [FromBody] SaveMonitoringRuleCommand command)
  {
    await monitoring.UpdateRule(id, command);
    return NoContent();
  }

  /// <summary>Увімкнути / вимкнути правило: { "enabled": false }.</summary>
  [HttpPut("rules/{id:guid}/enabled")]
  public async Task<IActionResult> SetEnabled(Guid id, [FromBody] SetEnabledRequest request)
  {
    await monitoring.SetEnabled(id, request.Enabled);
    return NoContent();
  }

  [HttpDelete("rules/{id:guid}")]
  public async Task<IActionResult> DeleteRule(Guid id)
  {
    await monitoring.DeleteRule(id);
    return NoContent();
  }

  /// <summary>Перевірити правило зараз; результат записується як звичайна перевірка (зі сповіщенням, якщо стан змінився).</summary>
  [HttpPost("rules/{id:guid}/check")]
  public async Task<ActionResult<MonitoringRuleDto>> CheckNow(Guid id, CancellationToken cancellationToken)
    => await monitoring.CheckNow(id, cancellationToken);

  /// <summary>Порахувати правило без збереження - яке значення і стан були б зараз.</summary>
  [HttpPost("rules/preview")]
  public async Task<ActionResult<MonitoringPreviewDto>> Preview([FromBody] SaveMonitoringRuleCommand command, CancellationToken cancellationToken)
    => await monitoring.Preview(command, cancellationToken);

  /// <summary>Сповіщення, новіші першими. before - CreatedAt останнього отриманого (для «показати ще»).</summary>
  [HttpGet("notifications")]
  public async Task<ActionResult<List<NotificationDto>>> GetNotifications(
    [FromQuery] int take = 50,
    [FromQuery] bool unreadOnly = false,
    [FromQuery] Guid? ruleId = null,
    [FromQuery] DateTime? before = null)
    => await monitoring.GetNotifications(take, unreadOnly, ruleId, before?.ToUniversalTime());

  [HttpGet("notifications/unread-count")]
  public async Task<ActionResult<UnreadCountDto>> CountUnread()
    => new UnreadCountDto { Count = await monitoring.CountUnread() };

  /// <summary>Позначити прочитаним: { "id": "…" } - одне, {} - усі.</summary>
  [HttpPost("notifications/read")]
  public async Task<IActionResult> MarkRead([FromBody] MarkReadRequest request)
  {
    await monitoring.MarkRead(request.Id);
    return NoContent();
  }

  public class SetEnabledRequest
  {
    public bool Enabled { get; set; }
  }

  public class MarkReadRequest
  {
    public Guid? Id { get; set; }
  }
}
