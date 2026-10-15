using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.ServerApiServices;

/// <summary>Моніторинг (api/monitoring): правила, перевірка, сповіщення.</summary>
public class MonitoringApiService(HttpClient httpClient, ApiContext context) : ApiServiceBase(httpClient, context)
{
  private const string BaseUrl = "api/monitoring";

  public Task<List<MonitoringRule>> GetRules(CancellationToken cancellationToken = default)
    => GetAsync<List<MonitoringRule>>($"{BaseUrl}/rules", cancellationToken);

  public Task<MonitoringRule> GetRule(Guid id)
    => GetAsync<MonitoringRule>($"{BaseUrl}/rules/{id}");

  public async Task<Guid> CreateRule(object body)
    => (await PostAsync<IdResponse>($"{BaseUrl}/rules", body)).Id;

  /// <param name="version">Версія правила, з якою відкрито редактор: змінене відтоді правило не перезаписується (409).</param>
  public Task UpdateRule(Guid id, object body, int version)
  {
    var json = System.Text.Json.JsonSerializer.SerializeToNode(body, JsonOptions)!.AsObject();
    json["version"] = version;
    return PutAsync($"{BaseUrl}/rules/{id}", json);
  }

  public Task SetEnabled(Guid id, bool enabled)
    => PutAsync($"{BaseUrl}/rules/{id}/enabled", new { enabled });

  public Task DeleteRule(Guid id)
    => DeleteAsync($"{BaseUrl}/rules/{id}");

  public Task<MonitoringRule> CheckNow(Guid id)
    => PostEmptyAsync<MonitoringRule>($"{BaseUrl}/rules/{id}/check");

  public Task<MonitoringPreview> Preview(object body, CancellationToken cancellationToken = default)
    => PostAsync<MonitoringPreview>($"{BaseUrl}/rules/preview", body, cancellationToken);

  public Task<List<NotificationItem>> GetNotifications(int take = 50, bool unreadOnly = false, Guid? ruleId = null, DateTime? before = null)
  {
    var query = $"take={take}&unreadOnly={unreadOnly.ToString().ToLowerInvariant()}";
    if (ruleId is { } id)
      query += $"&ruleId={id}";
    if (before is { } b)
      query += $"&before={Escape(b.ToUniversalTime().ToString("O"))}";

    return GetAsync<List<NotificationItem>>($"{BaseUrl}/notifications?{query}");
  }

  public async Task<int> CountUnread(CancellationToken cancellationToken = default)
    => (await GetAsync<CountResponse>($"{BaseUrl}/notifications/unread-count", cancellationToken)).Count;

  /// <summary>Позначити прочитаним одне сповіщення або всі (<paramref name="id"/> = null).</summary>
  public Task MarkRead(Guid? id = null)
    => PostNoContentAsync($"{BaseUrl}/notifications/read", new { id });

  private class IdResponse
  {
    public Guid Id { get; set; }
  }

  private class CountResponse
  {
    public int Count { get; set; }
  }
}
