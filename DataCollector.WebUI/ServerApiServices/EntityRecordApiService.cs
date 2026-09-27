using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.ServerApiServices;

public class EntityRecordApiService(HttpClient httpClient) : ApiServiceBase(httpClient)
{
  private static string RecordsUrl(Guid configId) => $"api/data-configs/{configId}/records";

  public Task<PagedResult<EntityInstance>> GetPage(
    Guid configId,
    int pageNumber,
    int pageSize,
    string? sortBy,
    bool sortDescending)
  {
    var query = $"?pageNumber={pageNumber}&pageSize={pageSize}&sortDescending={sortDescending.ToString().ToLowerInvariant()}";
    if (!string.IsNullOrEmpty(sortBy))
      query += $"&sortBy={Escape(sortBy)}";

    return GetAsync<PagedResult<EntityInstance>>(RecordsUrl(configId) + query);
  }

  public async Task<Guid> Create(Guid configId, Dictionary<string, object?> values)
  {
    var result = await PostAsync<IdResponse>(RecordsUrl(configId), new { values });
    return result.Id;
  }

  public Task Update(Guid configId, Guid recordId, Dictionary<string, object?> values)
    => PatchAsync($"{RecordsUrl(configId)}/{recordId}", new { values });

  public Task Delete(Guid configId, Guid recordId)
    => DeleteAsync($"{RecordsUrl(configId)}/{recordId}");

  private class IdResponse
  {
    public Guid Id { get; set; }
  }
}
