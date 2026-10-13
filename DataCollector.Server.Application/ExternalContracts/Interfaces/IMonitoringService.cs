using DataCollector.Server.Application.ExternalContracts.Models;

namespace DataCollector.Server.Application.ExternalContracts.Interfaces;

/// <summary>Правила моніторингу і сповіщення про зміну їхнього стану.</summary>
public interface IMonitoringService
{
  Task<List<MonitoringRuleDto>> GetRules();
  Task<MonitoringRuleDto> GetRule(Guid id);
  Task<Guid> CreateRule(SaveMonitoringRuleCommand command);
  Task UpdateRule(Guid id, SaveMonitoringRuleCommand command);
  Task SetEnabled(Guid id, bool enabled);
  Task DeleteRule(Guid id);

  /// <summary>Перевірити правило зараз (поза розкладом) і записати результат так само, як фонова перевірка.</summary>
  Task<MonitoringRuleDto> CheckNow(Guid id, CancellationToken cancellationToken = default);

  /// <summary>Порахувати правило без збереження: яке значення і стан були б зараз.</summary>
  Task<MonitoringPreviewDto> Preview(SaveMonitoringRuleCommand command, CancellationToken cancellationToken = default);

  Task<List<NotificationDto>> GetNotifications(int take, bool unreadOnly, Guid? ruleId, DateTime? before);
  Task<int> CountUnread();

  /// <summary>Позначити прочитаним одне сповіщення або всі (<paramref name="id"/> = null).</summary>
  Task MarkRead(Guid? id);
}

/// <summary>Робота фонового сервісу моніторингу (викликається з хоста за таймером).</summary>
public interface IMonitoringRunner
{
  /// <summary>Перевіряє правила, яким настав час; повертає кількість перевірених.</summary>
  Task<int> RunDue(CancellationToken cancellationToken);

  /// <summary>Надсилає сповіщення, що очікують доставки; повертає кількість спроб.</summary>
  Task<int> DeliverPending(CancellationToken cancellationToken);
}
