using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataCollector.WebUI.Services;

/// <summary>
/// Інтерфейс працює лише разом з API: перевірка стану інтерфейсу питає готовність API (/health/ready - API і база даних).
/// </summary>
public sealed class ApiHealthCheck(IHttpClientFactory httpClients, IConfiguration configuration) : IHealthCheck
{
  public const string HttpClientName = "health";

  public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
  {
    var client = httpClients.CreateClient(HttpClientName);
    client.BaseAddress = new Uri(configuration["ServerApi:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(5);

    try
    {
      using var response = await client.GetAsync("health/ready", cancellationToken);
      return response.IsSuccessStatusCode
        ? HealthCheckResult.Healthy("API і база даних доступні")
        : HealthCheckResult.Unhealthy($"API не готове ({(int)response.StatusCode})");
    }
    catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
    {
      return HealthCheckResult.Unhealthy("API недоступне");
    }
  }
}
