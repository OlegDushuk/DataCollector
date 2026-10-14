using Microsoft.Extensions.Logging;
using System.Text.Json;
using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Caching;
using DataCollector.Server.Application.Interfaces.Notifications;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Application.Services.Internal;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Monitoring;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.Application.Services;

/// <summary>
/// Моніторинг: правила, їх перевірка і сповіщення. Показник правила рахується тим самим рушієм агрегації,
/// що будує зведення й діаграми (<see cref="IEntityInstanceRepository.Aggregate"/>), - лише без групувань.
/// </summary>
public class MonitoringService(
  IMonitoringRuleRepository rules,
  INotificationRepository notifications,
  IEntityConfigCache entityConfigs,
  IEntityInstanceRepository entityInstanceRepository,
  INotificationSender sender,
  ILogger<MonitoringService>? logger = null
) : IMonitoringService, IMonitoringRunner
{
  /// <summary>Скільки правил перевіряється за один прохід фонового сервісу.</summary>
  public const int BatchSize = 50;

  /// <summary>Спроб доставки одного сповіщення, після чого - «не вдалося».</summary>
  public const int MaxDeliveryAttempts = 5;

  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  // ---------- Правила ----------

  public async Task<List<MonitoringRuleDto>> GetRules()
  {
    var records = await rules.GetAll();
    var result = new List<MonitoringRuleDto>();

    foreach (var record in records)
      result.Add(await ToDto(record));

    return result;
  }

  public async Task<MonitoringRuleDto> GetRule(Guid id) => await ToDto(await Load(id));

  public async Task<Guid> CreateRule(SaveMonitoringRuleCommand command)
  {
    var (definition, entity) = await Definition(command);
    var rule = MonitoringRule.Create(definition, entity);

    await rules.Create(rule);
    return rule.Id;
  }

  public async Task UpdateRule(Guid id, SaveMonitoringRuleCommand command)
  {
    var record = await Load(id);
    var (definition, entity) = await Definition(command);

    record.Rule.Change(definition, entity);
    await rules.Update(record.Rule);
  }

  public async Task SetEnabled(Guid id, bool enabled)
  {
    var record = await Load(id);
    record.Rule.Enable(enabled);
    await rules.Update(record.Rule);
  }

  public Task DeleteRule(Guid id) => rules.Delete(id);

  public async Task<MonitoringRuleDto> CheckNow(Guid id, CancellationToken cancellationToken = default)
  {
    var record = await Load(id);
    await Check(record.Rule, DateTime.UtcNow, cancellationToken);
    return await ToDto(await Load(id));
  }

  public async Task<MonitoringPreviewDto> Preview(SaveMonitoringRuleCommand command, CancellationToken cancellationToken = default)
  {
    var (definition, entity) = await Definition(command);
    var rule = MonitoringRule.Create(definition, entity);
    var (value, error, metricName) = await Evaluate(rule, DateTime.UtcNow, cancellationToken);

    var state = error != null || value is null
      ? RuleState.NoData
      : rule.Operator.Matches(value.Value, rule.Threshold) ? RuleState.Alert : RuleState.Ok;

    return new MonitoringPreviewDto
    {
      Value = value,
      State = StateCode(state),
      MetricName = metricName,
      Condition = rule.Describe(metricName),
      Error = error
    };
  }

  // ---------- Сповіщення ----------

  public Task<List<NotificationDto>> GetNotifications(int take, bool unreadOnly, Guid? ruleId, DateTime? before)
    => notifications.GetRecent(Math.Clamp(take, 1, 200), unreadOnly, ruleId, before);

  public Task<int> CountUnread() => notifications.CountUnread();

  public Task MarkRead(Guid? id) => notifications.MarkRead(id, DateTime.UtcNow);

  // ---------- Фонова робота ----------

  public async Task<int> RunDue(CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var due = await rules.ClaimDue(now, BatchSize);
    var failures = new List<Exception>();

    // Збій одного правила (наприклад, тайм-аут запиту) не зупиняє решту: їх уже взято в роботу.
    foreach (var record in due)
    {
      cancellationToken.ThrowIfCancellationRequested();

      try
      {
        await Check(record.Rule, now, cancellationToken);
      }
      catch (Exception e) when (e is not OperationCanceledException)
      {
        failures.Add(new InvalidOperationException($"Правило «{record.Rule.Name}» ({record.Rule.Id}): {e.Message}", e));
      }
    }

    if (failures.Count > 0)
      throw new AggregateException("Не вдалося перевірити частину правил моніторингу", failures);

    return due.Count;
  }

  public async Task<int> DeliverPending(CancellationToken cancellationToken)
  {
    if (!sender.IsConfigured)
      return 0;

    var pending = await notifications.GetPendingDelivery(BatchSize);

    foreach (var notification in pending)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var attempts = notification.DeliveryAttempts + 1;

      try
      {
        await sender.Send(notification, cancellationToken);
        await notifications.SetDelivery(notification.Id, DeliveryStatus.Sent, attempts, DateTime.UtcNow, null);
      }
      catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
      {
        // Канал недоступний - повтор на наступних проходах; після MaxDeliveryAttempts - «не вдалося».
        var status = attempts >= MaxDeliveryAttempts ? DeliveryStatus.Failed : DeliveryStatus.Pending;
        await notifications.SetDelivery(notification.Id, status, attempts, null, Truncate(e.Message, 1000));

        logger?.LogWarning("Сповіщення {NotificationId} не доставлено (спроба {Attempt} з {Max}): {Error}",
          notification.Id, attempts, MaxDeliveryAttempts, e.Message);
      }
    }

    return pending.Count;
  }

  /// <summary>Перевірка одного правила: показник -> новий стан -> запис стану і сповіщення (якщо стан змінився).</summary>
  private async Task Check(MonitoringRule rule, DateTime now, CancellationToken cancellationToken)
  {
    var (value, error, metricName) = await Evaluate(rule, now, cancellationToken);
    var notification = rule.ApplyCheck(value, error, now, metricName);
    var delivery = notification != null && sender.IsConfigured ? DeliveryStatus.Pending : DeliveryStatus.NotRequired;

    await rules.SaveCheck(rule, notification, delivery);

    if (notification != null)
      logger?.LogInformation("Правило «{Rule}»: {FromState} -> {ToState}, значення {Value}",
        rule.Name, notification.FromState, notification.ToState, notification.Value);
  }

  /// <summary>
  /// Показник правила на поточний момент. Помилки даних (поле видалено, модель видалено, умова вже не підходить
  /// до типу поля) не зупиняють моніторинг - вони стають станом «немає даних» з поясненням.
  /// </summary>
  private async Task<(decimal? Value, string? Error, string MetricName)> Evaluate(
    MonitoringRule rule, DateTime now, CancellationToken cancellationToken)
  {
    EntityConfig entity;
    try
    {
      entity = await entityConfigs.Get(rule.ConfigId.ToString());
    }
    catch (NotFoundException)
    {
      return (null, "модель правила видалено", "Показник");
    }

    try
    {
      var metric = rule.Metric(entity);
      var filters = RecordQueries.Filters(entity, ParseFilters(rule.Filters));
      if (rule.WindowFilter(entity, now) is { } window)
        filters.Add(window);

      var query = new AggregateQuery(filters, rule.Search, [], [metric], AggregateOrder.Keys, 1, null);
      var result = await entityInstanceRepository.Aggregate(entity, query, cancellationToken);

      var raw = result.Rows.Count > 0 ? result.Rows[0].Values[0] : null;
      return (ToDecimal(raw), null, metric.Name);
    }
    catch (DomainValidationException e)
    {
      return (null, e.Message, rule.FieldKey ?? "Показник");
    }
  }

  // ---------- Допоміжне ----------

  private async Task<RuleRecord> Load(Guid id)
    => await rules.GetById(id) ?? throw new NotFoundException("Правило не знайдене");

  private async Task<(RuleDefinition Definition, EntityConfig Entity)> Definition(SaveMonitoringRuleCommand command)
  {
    var entity = await entityConfigs.Get(command.Config);

    // Умови перевіряються одразу (як у запиті записів), щоб помилка була відповіддю 400, а не станом «немає даних».
    _ = RecordQueries.Filters(entity, command.Filters);

    var definition = new RuleDefinition(
      command.Name,
      command.IsEnabled,
      command.Filters is { Count: > 0 } filters ? JsonSerializer.Serialize(filters, JsonOptions) : null,
      command.Search,
      command.Function.ToAggregateFunction(),
      command.Field,
      command.TimeField ?? RecordField.CreatedAtKey,
      command.WindowMinutes,
      RuleOperators.Parse(command.Operator) ?? throw new DomainValidationException($"Невідома умова '{command.Operator}'. Доступні: gt, gte, lt, lte, eq, neq"),
      command.Threshold,
      command.ConsecutiveChecks,
      command.CheckIntervalSeconds);

    return (definition, entity);
  }

  private static List<RecordFilterDto>? ParseFilters(string? json)
    => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<List<RecordFilterDto>>(json, JsonOptions);

  private static decimal? ToDecimal(object? value) => value switch
  {
    null => null,
    decimal d => d,
    long l => l,
    int i => i,
    double d => (decimal)d,
    _ => null
  };

  private async Task<MonitoringRuleDto> ToDto(RuleRecord record)
  {
    var rule = record.Rule;
    string metricName;

    try
    {
      metricName = rule.Metric(await entityConfigs.Get(rule.ConfigId.ToString())).Name;
    }
    catch (Exception e) when (e is DomainValidationException or NotFoundException)
    {
      metricName = rule.FieldKey ?? "Показник";
    }

    return new MonitoringRuleDto
    {
      Id = rule.Id,
      ConfigId = rule.ConfigId,
      ConfigName = record.ConfigName,
      Name = rule.Name,
      IsEnabled = rule.IsEnabled,
      Filters = ParseFilters(rule.Filters) ?? [],
      Search = rule.Search,
      Function = rule.Function.ToString().ToLowerInvariant(),
      Field = rule.FieldKey,
      TimeField = rule.TimeField,
      WindowMinutes = rule.WindowMinutes,
      Operator = rule.Operator.Code(),
      Threshold = Trim(rule.Threshold),
      ConsecutiveChecks = rule.ConsecutiveChecks,
      CheckIntervalSeconds = rule.CheckIntervalSeconds,
      Condition = rule.Describe(metricName),
      State = StateCode(rule.State),
      StateSince = rule.StateSince,
      LastValue = rule.LastValue is { } v ? Trim(v) : null,
      LastCheckedAt = rule.LastCheckedAt,
      LastError = rule.LastError,
      NextCheckAt = rule.NextCheckAt,
      CreatedAt = rule.CreatedAt,
      UpdatedAt = rule.UpdatedAt
    };
  }

  public static string StateCode(RuleState state) => state switch
  {
    RuleState.Ok => "ok",
    RuleState.Alert => "alert",
    RuleState.NoData => "noData",
    _ => "unknown"
  };

  private static decimal Trim(decimal value) => value / 1.0000000000000000000000000000m;

  private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
