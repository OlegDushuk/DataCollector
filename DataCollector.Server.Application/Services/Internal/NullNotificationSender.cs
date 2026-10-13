using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Notifications;

namespace DataCollector.Server.Application.Services.Internal;

/// <summary>Зовнішній канал не підключено: сповіщення видно лише в інтерфейсі.</summary>
internal sealed class NullNotificationSender : INotificationSender
{
  public bool IsConfigured => false;

  public Task Send(NotificationDto notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
