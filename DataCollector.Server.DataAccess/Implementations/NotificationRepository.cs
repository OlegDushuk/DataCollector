using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Application.Services;
using DataCollector.Server.DataAccess.Common;
using DataCollector.Server.DataAccess.Rows;
using DataCollector.Server.Domain.Monitoring;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

public class NotificationRepository(IConfiguration config)
  : RepositoryBase(config, "Notifications"), INotificationRepository
{
  public async Task<List<NotificationDto>> GetRecent(int take, bool unreadOnly, Guid? ruleId, DateTime? before)
  {
    var rows = await QueryListAsync<NotificationRow>("GetRecent", new { Take = take, UnreadOnly = unreadOnly, RuleId = ruleId, Before = before });
    return rows.Select(ToDto).ToList();
  }

  public async Task<int> CountUnread() => await QueryFirstOrDefaultAsync<int>("CountUnread");

  public Task MarkRead(Guid? id, DateTime nowUtc) => ExecuteAsync("MarkRead", new { Id = id, Now = nowUtc });

  public async Task<List<NotificationDto>> GetPendingDelivery(int take)
    => (await QueryListAsync<NotificationRow>("GetPendingDelivery", new { Take = take })).Select(ToDto).ToList();

  public Task SetDelivery(Guid id, DeliveryStatus status, int attempts, DateTime? deliveredAt, string? error)
    => ExecuteAsync("SetDelivery", new
    {
      Id = id,
      DeliveryStatus = (byte)status,
      DeliveryAttempts = attempts,
      DeliveredAt = deliveredAt,
      DeliveryError = error
    });

  private static NotificationDto ToDto(NotificationRow row) => new()
  {
    Id = row.Id,
    RuleId = row.RuleId,
    RuleName = row.RuleName,
    ConfigName = row.ConfigName,
    CreatedAt = AsUtc(row.CreatedAt),
    FromState = MonitoringService.StateCode((RuleState)row.FromState),
    ToState = MonitoringService.StateCode((RuleState)row.ToState),
    Value = row.Value is { } v ? v / 1.0000000000000000000000000000m : null,
    Threshold = row.Threshold / 1.0000000000000000000000000000m,
    Message = row.Message,
    ReadAt = AsUtc(row.ReadAt),
    Delivery = (DeliveryStatus)row.DeliveryStatus switch
    {
      DeliveryStatus.Pending => "pending",
      DeliveryStatus.Sent => "sent",
      DeliveryStatus.Failed => "failed",
      _ => "notRequired"
    },
    DeliveryAttempts = row.DeliveryAttempts,
    DeliveredAt = AsUtc(row.DeliveredAt),
    DeliveryError = row.DeliveryError
  };
}
