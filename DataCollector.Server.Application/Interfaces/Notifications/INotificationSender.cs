using DataCollector.Server.Application.ExternalContracts.Models;

namespace DataCollector.Server.Application.Interfaces.Notifications;

/// <summary>Зовнішній канал сповіщень (вебхук, Telegram). Реалізація - у хості API (потрібен HttpClient).</summary>
public interface INotificationSender
{
  /// <summary>Канал налаштовано - нові сповіщення стають у чергу доставки.</summary>
  bool IsConfigured { get; }

  /// <summary>Надіслати; помилка - виняток (буде повторна спроба).</summary>
  Task Send(NotificationDto notification, CancellationToken cancellationToken);
}
