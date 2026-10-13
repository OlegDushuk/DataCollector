using Dapper;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Common;
using DataCollector.Server.DataAccess.Rows;
using DataCollector.Server.Domain.Monitoring;
using DataCollector.Server.Domain.Querying;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

public class MonitoringRuleRepository(IConfiguration config)
  : RepositoryBase(config, "MonitoringRules"), IMonitoringRuleRepository
{
  public async Task<List<RuleRecord>> GetAll()
    => (await QueryListAsync<MonitoringRuleRow>("GetAll")).Select(ToRecord).ToList();

  public async Task<RuleRecord?> GetById(Guid id)
    => await QueryFirstOrDefaultAsync<MonitoringRuleRow>("GetById", new { Id = id }) is { } row ? ToRecord(row) : null;

  public Task Create(MonitoringRule rule)
  {
    var parameters = Definition(rule);
    parameters.Add("CreatedAt", rule.CreatedAt);
    return ExecuteAsync("Create", parameters);
  }

  public Task Update(MonitoringRule rule)
  {
    var parameters = Definition(rule);
    parameters.Add("UpdatedAt", rule.UpdatedAt ?? DateTime.UtcNow);
    return ExecuteAsync("Update", parameters);
  }

  public Task Delete(Guid id) => ExecuteAsync("Delete", new { Id = id });

  public async Task<List<RuleRecord>> ClaimDue(DateTime nowUtc, int take)
    => (await QueryListAsync<MonitoringRuleRow>("ClaimDue", new { Now = nowUtc, Take = take })).Select(ToRecord).ToList();

  public async Task<bool> SaveCheck(MonitoringRule rule, NotificationDraft? notification, DeliveryStatus delivery)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", rule.Id);
    parameters.Add("Version", rule.Version);
    parameters.Add("State", (byte)rule.State);
    parameters.Add("StateSince", rule.StateSince);
    parameters.Add("LastValue", rule.LastValue);
    parameters.Add("LastCheckedAt", rule.LastCheckedAt);
    parameters.Add("LastError", rule.LastError);
    parameters.Add("PendingState", rule.PendingState is { } pending ? (byte?)pending : null);
    parameters.Add("PendingCount", rule.PendingCount);
    parameters.Add("NotificationId", notification?.Id);
    parameters.Add("FromState", notification is null ? null : (byte?)notification.FromState);
    parameters.Add("ToState", notification is null ? null : (byte?)notification.ToState);
    parameters.Add("Value", notification?.Value);
    parameters.Add("Threshold", notification?.Threshold);
    parameters.Add("Message", notification?.Message);
    parameters.Add("DeliveryStatus", (byte)delivery);

    return await QueryFirstOrDefaultAsync<bool>("SaveCheck", parameters);
  }

  private static DynamicParameters Definition(MonitoringRule rule)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", rule.Id);
    parameters.Add("ConfigId", rule.ConfigId);
    parameters.Add("Name", rule.Name);
    parameters.Add("IsEnabled", rule.IsEnabled);
    parameters.Add("Filters", rule.Filters);
    parameters.Add("Search", rule.Search);
    parameters.Add("Function", rule.Function.ToString().ToLowerInvariant());
    parameters.Add("FieldKey", rule.FieldKey);
    parameters.Add("TimeField", rule.TimeField);
    parameters.Add("WindowMinutes", rule.WindowMinutes);
    parameters.Add("Operator", rule.Operator.Code());
    parameters.Add("Threshold", rule.Threshold);
    parameters.Add("ConsecutiveChecks", rule.ConsecutiveChecks);
    parameters.Add("CheckIntervalSeconds", rule.CheckIntervalSeconds);
    return parameters;
  }

  private static RuleRecord ToRecord(MonitoringRuleRow row)
  {
    var definition = new RuleDefinition(
      row.Name,
      row.IsEnabled,
      row.Filters,
      row.Search,
      Enum.Parse<AggregateFunction>(row.Function, ignoreCase: true),
      row.FieldKey,
      row.TimeField,
      row.WindowMinutes,
      RuleOperators.Parse(row.Operator) ?? RuleOperator.GreaterThan,
      row.Threshold,
      row.ConsecutiveChecks,
      row.CheckIntervalSeconds);

    var state = new RuleSnapshot(
      (RuleState)row.State,
      AsUtc(row.StateSince),
      row.LastValue,
      AsUtc(row.LastCheckedAt),
      row.LastError,
      row.PendingState is { } pending ? (RuleState)pending : null,
      row.PendingCount,
      AsUtc(row.NextCheckAt),
      row.Version,
      AsUtc(row.UpdatedAt));

    var rule = MonitoringRule.Restore(row.Id, AsUtc(row.CreatedAt), definition, row.ConfigId, state);
    return new RuleRecord(rule, row.ConfigName);
  }
}
