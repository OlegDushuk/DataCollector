using DataCollector.WebUI.Entities;
using DataCollector.WebUI.Enums;

namespace DataCollector.WebUI.ServerApiServices;

public class EntityConfigApiService(HttpClient httpClient) : ApiServiceBase(httpClient)
{
  private const string BaseUrl = "api/data-configs";

  public Task<List<EntityConfigListItem>> GetAll()
    => GetAsync<List<EntityConfigListItem>>(BaseUrl);

  /// <param name="config">Id або ключ моделі.</param>
  public Task<EntityConfig> Get(string config)
    => GetAsync<EntityConfig>($"{BaseUrl}/{Escape(config)}");

  public async Task<Guid> Create(string name, string key, IEnumerable<NewProperty> properties)
  {
    var result = await PostAsync<IdResponse>(BaseUrl, new
    {
      name,
      key,
      properties = properties.Select(p => new { p.Name, p.Key, p.DataType })
    });

    return result.Id;
  }

  public Task Edit(Guid id, string? name, string? key)
    => PatchAsync($"{BaseUrl}/{id}", new { name, key });

  public Task Delete(Guid id)
    => DeleteAsync($"{BaseUrl}/{id}");

  public Task<List<Guid>> AddProperties(Guid configId, IEnumerable<NewProperty> properties)
    => PostAsync<List<Guid>>($"{BaseUrl}/{configId}/properties", new
    {
      properties = properties.Select(p => new { p.Name, p.Key, p.DataType })
    });

  public Task EditProperty(Guid configId, Guid propertyId, string? name, string? key)
    => PatchAsync($"{BaseUrl}/{configId}/properties/{propertyId}", new { name, key });

  public Task DeleteProperty(Guid configId, Guid propertyId)
    => DeleteAsync($"{BaseUrl}/{configId}/properties/{propertyId}");

  public class NewProperty
  {
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public PropertyDataType DataType { get; set; }

    /// <summary>Ключ змінювали вручну - більше не генеруємо його з назви.</summary>
    public bool KeyTouched { get; set; }
  }

  private class IdResponse
  {
    public Guid Id { get; set; }
  }
}
