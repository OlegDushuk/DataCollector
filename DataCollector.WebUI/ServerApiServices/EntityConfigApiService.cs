using System.Text;
using System.Text.Json;
using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.ServerApiServices;

public class EntityConfigApiService
{
  private readonly HttpClient _httpClient;
  
  public EntityConfigApiService()
  {
    _httpClient = new HttpClient();
    _httpClient.BaseAddress = new Uri("http://localhost:5010/api/data-config");
  }

  public async Task Create(EntityConfig config)
  {
    var content = new StringContent(
      JsonSerializer.Serialize(config),
      Encoding.UTF8,
      "application/json");
    
    var res = await _httpClient.PostAsync("", content);
  }
}