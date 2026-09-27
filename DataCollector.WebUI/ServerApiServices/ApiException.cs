using System.Net;

namespace DataCollector.WebUI.ServerApiServices;

/// <summary>
/// Помилка від API (текст береться з ProblemDetails) або недоступність сервера.
/// </summary>
public class ApiException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
  public HttpStatusCode? StatusCode { get; } = statusCode;
  public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
}
