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

  protected async Task<T> GetAsync<T>(string url)
  {
    var response = await Send(() => httpClient.GetAsync(url));
    return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
  }

  protected async Task<TResult> PostAsync<TResult>(string url, object body)
  {
    var response = await Send(() => httpClient.PostAsJsonAsync(url, body, JsonOptions));
    return (await response.Content.ReadFromJsonAsync<TResult>(JsonOptions))!;
  }

  protected async Task PatchAsync(string url, object body)
  {
    await Send(() => httpClient.PatchAsJsonAsync(url, body, JsonOptions));
  }

  protected async Task DeleteAsync(string url)
  {
    await Send(() => httpClient.DeleteAsync(url));
  }

  protected static string Escape(string value) => Uri.EscapeDataString(value);

  private static async Task<HttpResponseMessage> Send(Func<Task<HttpResponseMessage>> request)
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
