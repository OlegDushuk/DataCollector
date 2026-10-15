using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.ServerApiServices;

public class EntityRecordApiService(HttpClient httpClient, ApiContext context) : ApiServiceBase(httpClient, context)
{
  private static string RecordsUrl(Guid configId) => $"api/data-configs/{configId}/records";

  /// <summary>
  /// Сторінка записів з умовами фільтра (POST .../records/query).
  /// </summary>
  /// <param name="filters">Умови у форматі API (див. <see cref="RecordFilterForm.ToApi"/>).</param>
  /// <param name="fields">Поля, значення яких повернути; null - усі (див. <see cref="TableColumns.ApiFields"/>).</param>
  public Task<PagedResult<EntityInstance>> Query(
    Guid configId,
    IEnumerable<object> filters,
    string? search,
    int pageNumber,
    int pageSize,
    string? sortBy,
    bool sortDescending,
    IReadOnlyCollection<string>? fields,
    CancellationToken cancellationToken = default)
  {
    var body = new
    {
      filters,
      search,
      sortBy,
      sortDescending,
      pageNumber,
      pageSize,
      fields
    };

    return PostAsync<PagedResult<EntityInstance>>($"{RecordsUrl(configId)}/query", body, cancellationToken);
  }

  /// <summary>
  /// Групування записів і показники по групах (POST .../records/aggregate).
  /// Межі днів і місяців рахуються в часовому поясі сервера інтерфейсу - так само, як показуються дати.
  /// </summary>
  public Task<AggregateResult> Aggregate(
    Guid configId,
    IEnumerable<object> filters,
    string? search,
    IEnumerable<object> groupBy,
    IEnumerable<object> metrics,
    string orderBy,
    CancellationToken cancellationToken = default,
    int limit = 500)
  {
    var body = new
    {
      filters,
      search,
      groupBy,
      metrics,
      orderBy,
      limit,
      timeZone = TimeZoneInfo.Local.Id
    };

    return PostAsync<AggregateResult>($"{RecordsUrl(configId)}/aggregate", body, cancellationToken);
  }

  /// <summary>
  /// Файл із записами (POST .../records/export): ті самі умови, пошук і сортування, що в таблиці,
  /// колонки - видимі колонки таблиці в їхньому порядку.
  /// </summary>
  /// <param name="format">"csv", "xlsx", "json" або "xml".</param>
  public Task<DownloadedFile> ExportRecords(
    Guid configId,
    string format,
    IEnumerable<object> filters,
    string? search,
    string? sortBy,
    bool sortDescending,
    IReadOnlyCollection<string>? fields,
    CancellationToken cancellationToken = default)
  {
    var body = new
    {
      format,
      filters,
      search,
      sortBy,
      sortDescending,
      fields,
      timeZone = TimeZoneInfo.Local.Id
    };

    return PostForFileAsync($"{RecordsUrl(configId)}/export", body, cancellationToken);
  }

  /// <summary>Файл зі зведенням (POST .../records/aggregate/export): ті самі групування й показники, усі групи.</summary>
  public Task<DownloadedFile> ExportAggregate(
    Guid configId,
    string format,
    IEnumerable<object> filters,
    string? search,
    IEnumerable<object> groupBy,
    IEnumerable<object> metrics,
    string orderBy,
    CancellationToken cancellationToken = default)
  {
    var body = new
    {
      format,
      filters,
      search,
      groupBy,
      metrics,
      orderBy,
      timeZone = TimeZoneInfo.Local.Id
    };

    return PostForFileAsync($"{RecordsUrl(configId)}/aggregate/export", body, cancellationToken);
  }

  /// <summary>Запис з усіма полями (таблиця може містити лише частину полів).</summary>
  public Task<EntityInstance> Get(Guid configId, Guid recordId, CancellationToken cancellationToken = default)
    => GetAsync<EntityInstance>($"{RecordsUrl(configId)}/{recordId}", cancellationToken);

  public async Task<Guid> Create(Guid configId, IReadOnlyDictionary<string, string?> values)
  {
    var result = await PostAsync<IdResponse>(RecordsUrl(configId), new { values });
    return result.Id;
  }

  /// <param name="version">Версія запису, яку бачив користувач: змінений відтоді запис не перезаписується (409).</param>
  public Task Update(Guid configId, Guid recordId, IReadOnlyDictionary<string, string?> values, long? version)
    => PatchAsync($"{RecordsUrl(configId)}/{recordId}", new { values, version });

  public Task Delete(Guid configId, Guid recordId, long? version)
    => DeleteAsync($"{RecordsUrl(configId)}/{recordId}", version);

  /// <summary>
  /// Імпорт записів з файлу (POST .../records/import). <paramref name="dryRun"/> - лише перевірка:
  /// звіт про те, що буде створено й оновлено, і помилки по рядках, без запису.
  /// Дати з часом без зміщення (Excel) читаються в часовому поясі сервера інтерфейсу - як і показуються.
  /// </summary>
  public Task<ImportResult> Import(
    Guid configId,
    byte[] file,
    string fileName,
    ImportOptions options,
    bool dryRun,
    CancellationToken cancellationToken = default)
  {
    var content = new MultipartFormDataContent();

    var fileContent = new ByteArrayContent(file);
    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
    content.Add(fileContent, "file", fileName);

    content.Add(new StringContent(dryRun ? "true" : "false"), "dryRun");
    content.Add(new StringContent(options.UpdateExisting ? "true" : "false"), "updateExisting");
    content.Add(new StringContent(options.SkipInvalid ? "true" : "false"), "skipInvalid");
    content.Add(new StringContent(options.KeepCreatedAt ? "true" : "false"), "keepCreatedAt");
    content.Add(new StringContent(TimeZoneInfo.Local.Id), "timeZone");

    return PostFormAsync<ImportResult>($"{RecordsUrl(configId)}/import", content, cancellationToken);
  }

  private class IdResponse
  {
    public Guid Id { get; set; }
  }
}
