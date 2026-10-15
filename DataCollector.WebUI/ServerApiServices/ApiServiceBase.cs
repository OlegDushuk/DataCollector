using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataCollector.WebUI.Services;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.ServerApiServices;

/// <summary>
/// Те, що потрібно кожному запиту до API в межах вікна: токен поточного користувача, перехід на вхід,
/// коли сеанс закінчився, і журнал.
/// </summary>
public sealed class ApiContext(CurrentUser user, NavigationManager navigation, ILogger<ApiContext> logger)
{
  public CurrentUser User { get; } = user;
  public ILogger Logger { get; } = logger;

  /// <summary>Токен більше не приймається (закінчився, змінено пароль або роль) - на сторінку входу.</summary>
  public void SessionExpired()
  {
    try
    {
      var returnUrl = "/" + navigation.ToBaseRelativePath(navigation.Uri);
      navigation.NavigateTo($"login?expired=1&returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
    }
    catch (InvalidOperationException)
    {
      // Поза рендерингом компонента переходити нікуди - вистачить повідомлення про помилку.
    }
  }
}

public abstract class ApiServiceBase(HttpClient httpClient, ApiContext context)
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
    using var response = await Send(HttpMethod.Get, url, null, cancellationToken);
    return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken))!;
  }

  protected async Task<TResult> PostAsync<TResult>(string url, object body, CancellationToken cancellationToken = default)
  {
    using var response = await Send(HttpMethod.Post, url, Json(body), cancellationToken);
    return (await response.Content.ReadFromJsonAsync<TResult>(JsonOptions, cancellationToken))!;
  }

  /// <summary>POST multipart/form-data (файл і поля форми), наприклад імпорт.</summary>
  protected async Task<TResult> PostFormAsync<TResult>(string url, MultipartFormDataContent content, CancellationToken cancellationToken = default)
  {
    using var response = await Send(HttpMethod.Post, url, content, cancellationToken);
    return (await response.Content.ReadFromJsonAsync<TResult>(JsonOptions, cancellationToken))!;
  }

  /// <summary>
  /// POST, у відповідь на який приходить файл (експорт). Назва файлу - із Content-Disposition,
  /// кількість рядків і ознака обрізання - із заголовків X-Export-*.
  /// </summary>
  protected async Task<DownloadedFile> PostForFileAsync(string url, object body, CancellationToken cancellationToken = default)
  {
    using var response = await Send(HttpMethod.Post, url, Json(body), cancellationToken);
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
    using var _ = await Send(HttpMethod.Patch, url, Json(body));
  }

  /// <summary>POST, відповідь на який - лише код успіху (204).</summary>
  protected async Task PostNoContentAsync(string url, object body)
  {
    using var _ = await Send(HttpMethod.Post, url, Json(body));
  }

  protected async Task PutAsync(string url, object body)
  {
    using var _ = await Send(HttpMethod.Put, url, Json(body));
  }

  /// <summary>PUT, у відповідь на який API повертає нову версію об'єкта в заголовку ETag (null - заголовка немає).</summary>
  protected async Task<long?> PutForVersionAsync(string url, object body)
  {
    using var response = await Send(HttpMethod.Put, url, Json(body));
    return long.TryParse(response.Headers.ETag?.Tag.Trim('"'), out var version) ? version : null;
  }

  /// <summary>POST без тіла запиту; відповідь - невеликий JSON.</summary>
  protected async Task<TResult> PostEmptyAsync<TResult>(string url)
  {
    using var response = await Send(HttpMethod.Post, url, null);
    return (await response.Content.ReadFromJsonAsync<TResult>(JsonOptions))!;
  }

  /// <summary>POST без тіла запиту і без тіла відповіді (204).</summary>
  protected async Task PostEmptyAsync(string url)
  {
    using var _ = await Send(HttpMethod.Post, url, null);
  }

  /// <param name="ifMatch">Версія об'єкта, яку бачив користувач: якщо його відтоді змінили, API відповість 409.</param>
  protected async Task DeleteAsync(string url, long? ifMatch = null)
  {
    using var _ = await Send(HttpMethod.Delete, url, null, ifMatch: ifMatch);
  }

  protected static string Escape(string value) => Uri.EscapeDataString(value);

  private static JsonContent Json(object body) => JsonContent.Create(body, options: JsonOptions);

  private async Task<HttpResponseMessage> Send(
    HttpMethod method,
    string url,
    HttpContent? content,
    CancellationToken cancellationToken = default,
    long? ifMatch = null)
  {
    using var request = new HttpRequestMessage(method, url) { Content = content };

    if (ifMatch is { } version)
      request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");

    // Кожен запит - від імені користувача, що працює з інтерфейсом: API саме перевіряє його права.
    var token = await context.User.AccessToken();
    if (token != null)
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    HttpResponseMessage response;

    try
    {
      response = await httpClient.SendAsync(request, cancellationToken);
    }
    catch (HttpRequestException e)
    {
      context.Logger.LogWarning(e, "API недоступне: {Method} {Url}", method, url);
      throw new ApiException("Не вдалося з'єднатися з сервером API. Перевірте, що DataCollector.Server.API запущений.");
    }
    // Скасування, якого ми не просили, - це тайм-аут HttpClient.
    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      context.Logger.LogWarning("API не відповіло вчасно: {Method} {Url}", method, url);
      throw new ApiException("Сервер API не відповів вчасно. Спробуйте ще раз.");
    }

    if (response.IsSuccessStatusCode)
      return response;

    using (response)
    {
      var status = response.StatusCode;
      var message = status switch
      {
        // Токен був, але API його не прийняло: сеанс закінчився або користувачу змінили пароль/роль.
        HttpStatusCode.Unauthorized when token != null => "Сеанс завершився - увійдіть знову",
        HttpStatusCode.Forbidden => "Недостатньо прав для цієї дії",
        _ => await ReadError(response)
      };

      if ((int)status >= 500)
        context.Logger.LogError("API повернуло помилку {Status}: {Method} {Url} - {Message}", (int)status, method, url, message);
      else
        context.Logger.LogInformation("API відхилило запит {Status}: {Method} {Url} - {Message}", (int)status, method, url, message);

      if (status == HttpStatusCode.Unauthorized && token != null)
        context.SessionExpired();

      throw new ApiException(message, status);
    }
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
    catch (Exception e) when (e is JsonException or NotSupportedException)
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
