using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataCollector.WebUI.ServerApiServices;

public abstract class ApiServiceBase(HttpClient httpClient)
{
  protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
  {
    Converters = { new JsonStringEnumConverter() }
  };

  /// <remarks>
  /// Скасування через <paramref name="cancellationToken"/> обриває HTTP-запит, і API зупиняє запит до бази.
  /// Виклик при цьому завершується <see cref="OperationCanceledException"/>, а не <see cref="ApiException"/>.
  /// </remarks>
  protected async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken = default)
  {
    var response = await Send(() => httpClient.GetAsync(url, cancellationToken), cancellationToken);
    return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken))!;
  }

  protected async Task<TResult> PostAsync<TResult>(string url, object body, CancellationToken cancellationToken = default)
  {
    var response = await Send(() => httpClient.PostAsJsonAsync(url, body, JsonOptions, cancellationToken), cancellationToken);
    return (await response.Content.ReadFromJsonAsync<TResult>(JsonOptions, cancellationToken))!;
  }

  /// <summary>
  /// POST, у відповідь на який приходить файл (експорт). Назва файлу - із Content-Disposition,
  /// кількість рядків і ознака обрізання - із заголовків X-Export-*.
  /// </summary>
  protected async Task<DownloadedFile> PostForFileAsync(string url, object body, CancellationToken cancellationToken = default)
  {
    var response = await Send(() => httpClient.PostAsJsonAsync(url, body, JsonOptions, cancellationToken), cancellationToken);
    var content = response.Content.Headers;

    return new DownloadedFile
    {
      FileName = content.ContentDisposition?.FileNameStar ?? content.ContentDisposition?.FileName?.Trim('"') ?? "export",
      ContentType = content.ContentType?.MediaType ?? "application/octet-stream",
      Content = await response.Content.ReadAsByteArrayAsync(cancellationToken),
      RowCount = int.TryParse(Header(response, "X-Export-Rows"), out var rows) ? rows : null,
      Truncated = Header(response, "X-Export-Truncated") == "true"
    };
  }

  private static string? Header(HttpResponseMessage response, string name)
    => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

  protected async Task PatchAsync(string url, object body)
  {
    await Send(() => httpClient.PatchAsJsonAsync(url, body, JsonOptions));
  }

  protected async Task PutAsync(string url, object body)
  {
    await Send(() => httpClient.PutAsJsonAsync(url, body, JsonOptions));
  }

  /// <summary>POST без тіла відповіді, яке нас цікавить (лише код успіху) або з невеликою відповіддю.</summary>
  protected async Task<TResult> PostEmptyAsync<TResult>(string url)
  {
    var response = await Send(() => httpClient.PostAsync(url, content: null));
    return (await response.Content.ReadFromJsonAsync<TResult>(JsonOptions))!;
  }

  protected async Task DeleteAsync(string url)
  {
    await Send(() => httpClient.DeleteAsync(url));
  }

  protected static string Escape(string value) => Uri.EscapeDataString(value);

  private static async Task<HttpResponseMessage> Send(
    Func<Task<HttpResponseMessage>> request,
    CancellationToken cancellationToken = default)
  {
    HttpResponseMessage response;

    try
    {
      response = await request();
    }
    catch (HttpRequestException)
    {
      throw new ApiException("Не вдалося з'єднатися з сервером API. Перевірте, що DataCollector.Server.API запущений.");
    }
    // Скасування, якого ми не просили, - це тайм-аут HttpClient.
    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      throw new ApiException("Сервер API не відповів вчасно. Спробуйте ще раз.");
    }

    if (response.IsSuccessStatusCode)
      return response;

    throw new ApiException(await ReadError(response), response.StatusCode);
  }

  private static async Task<string> ReadError(HttpResponseMessage response)
  {
    try
    {
      var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);

      if (!string.IsNullOrWhiteSpace(problem?.Detail))
        return problem.Detail;

      if (problem?.Errors is { Count: > 0 })
        return string.Join("; ", problem.Errors.SelectMany(e => e.Value));

      if (!string.IsNullOrWhiteSpace(problem?.Title))
        return problem.Title;
    }
    catch (JsonException)
    {
      // Відповідь не у форматі ProblemDetails.
    }

    return $"Помилка сервера ({(int)response.StatusCode})";
  }

  private class ProblemDetailsResponse
  {
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
  }
}
