using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Domain.Monitoring;

namespace DataCollector.Server.Application.Interfaces.Repositories;

/// <summary>Правило разом з назвою його моделі (для списків і сповіщень).</summary>
public sealed record RuleRecord(MonitoringRule Rule, string ConfigName);

public interface IMonitoringRuleRepository
{
  Task<List<RuleRecord>> GetAll();
  Task<RuleRecord?> GetById(Guid id);
  Task Create(MonitoringRule rule);

  /// <summary>
  /// Нове визначення (стан зберігається, Version збільшується, перевірка - одразу), якщо версія в сховищі
  /// досі <paramref name="expectedVersion"/> (інакше - конфлікт).
  /// </summary>
  Task Update(MonitoringRule rule, int expectedVersion);

  Task Delete(Guid id);

  /// <summary>Правила, яким настав час перевірки; наступна перевірка призначається в тому ж запиті.</summary>
  Task<List<RuleRecord>> ClaimDue(DateTime nowUtc, int take);

  /// <summary>
  /// Записує стан після перевірки і (якщо є) сповіщення в одній транзакції.
  /// false - визначення правила змінили під час перевірки, результат відкинуто.
  /// </summary>
  Task<bool> SaveCheck(MonitoringRule rule, NotificationDraft? notification, DeliveryStatus delivery);
}

public interface INotificationRepository
{
  Task<List<NotificationDto>> GetRecent(int take, bool unreadOnly, Guid? ruleId, DateTime? before);
  Task<int> CountUnread();
  Task MarkRead(Guid? id, DateTime nowUtc);
  Task<List<NotificationDto>> GetPendingDelivery(int take);
  Task SetDelivery(Guid id, DeliveryStatus status, int attempts, DateTime? deliveredAt, string? error);
}
