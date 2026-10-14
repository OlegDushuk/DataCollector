using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.ServerApiServices;

/// <summary>Дашборди (api/dashboards): список, перегляд, створення, повне збереження, копія, видалення.</summary>
public class DashboardApiService(HttpClient httpClient, ApiContext context) : ApiServiceBase(httpClient, context)
{
  private const string BaseUrl = "api/dashboards";

  public Task<List<DashboardListItem>> GetAll()
    => GetAsync<List<DashboardListItem>>(BaseUrl);

  public Task<Dashboard> Get(Guid id, CancellationToken cancellationToken = default)
    => GetAsync<Dashboard>($"{BaseUrl}/{id}", cancellationToken);

  public async Task<Guid> Create(string name, string? description, IEnumerable<DashboardWidget>? widgets = null)
  {
    var result = await PostAsync<IdResponse>(BaseUrl, Body(name, description, 0, widgets ?? []));
    return result.Id;
  }

  /// <summary>Зберігає дашборд повністю: віджети, яких немає в списку, сервер видаляє.</summary>
  public Task Save(Dashboard dashboard)
    => PutAsync($"{BaseUrl}/{dashboard.Id}", Body(dashboard.Name, dashboard.Description, dashboard.RefreshSeconds, dashboard.Widgets));

  public async Task<Guid> Copy(Guid id)
  {
    var result = await PostEmptyAsync<IdResponse>($"{BaseUrl}/{id}/copy");
    return result.Id;
  }

  public Task Delete(Guid id)
    => DeleteAsync($"{BaseUrl}/{id}");

  private static object Body(string name, string? description, int refreshSeconds, IEnumerable<DashboardWidget> widgets) => new
  {
    name,
    description,
    refreshSeconds,
    widgets = widgets.Select(w => new
    {
      id = w.Id,
      configId = w.ConfigId,
      type = w.Type,
      title = w.Title,
      x = w.X,
      y = w.Y,
      w = w.W,
      h = w.H,
      settings = w.Settings
    })
  };

  private class IdResponse
  {
    public Guid Id { get; set; }
  }
}
