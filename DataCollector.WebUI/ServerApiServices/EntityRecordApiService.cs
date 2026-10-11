using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.ServerApiServices;

public class EntityRecordApiService(HttpClient httpClient) : ApiServiceBase(httpClient)
{
  private static string RecordsUrl(Guid configId) => $"api/data-configs/{configId}/records";

  /// <summary>
  /// Сторінка записів з умовами фільтра (POST .../records/query).
  /// </summary>
  /// <param name="filters">Умови у форматі API (див. <see cref="RecordFilterForm.ToApi"/>).</param>
  public Task<PagedResult<EntityInstance>> Query(
    Guid configId,
    IEnumerable<object> filters,
    string? search,
    int pageNumber,
    int pageSize,
    string? sortBy,
    bool sortDescending,
    CancellationToken cancellationToken = default)
  {
    var body = new
    {
      filters,
      search,
      sortBy,
      sortDescending,
      pageNumber,
      pageSize
    };

    return PostAsync<PagedResult<EntityInstance>>($"{RecordsUrl(configId)}/query", body, cancellationToken);
  }

  public async Task<Guid> Create(Guid configId, IReadOnlyDictionary<string, string?> values)
  {
    var result = await PostAsync<IdResponse>(RecordsUrl(configId), new { values });
    return result.Id;
  }

  public Task Update(Guid configId, Guid recordId, IReadOnlyDictionary<string, string?> values)
    => PatchAsync($"{RecordsUrl(configId)}/{recordId}", new { values });

  public Task Delete(Guid configId, Guid recordId)
    => DeleteAsync($"{RecordsUrl(configId)}/{recordId}");

  private class IdResponse
  {
    public Guid Id { get; set; }
  }
}
