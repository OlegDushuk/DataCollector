using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DataCollector.Tests.Integration;

/// <summary>Перевірка стану, опис API, ліміти запитів.</summary>
public class InfrastructureTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
  [Fact]
  public async Task Health_Live_And_Ready_WithoutSignIn()
  {
    var client = factory.CreateClient();

    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("health/live")).StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("health/ready")).StatusCode);

    var report = await client.GetFromJsonAsync<JsonElement>("health");
    Assert.Equal("Healthy", report.GetProperty("checks").GetProperty("database").GetProperty("status").GetString());
  }

  [Fact]
  public async Task Health_DatabaseDown_Ready503_LiveStill200()
  {
    var client = factory.CreateClient();
    factory.Store.DatabaseDown = true;

    try
    {
      Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("health/ready")).StatusCode);
      Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("health/live")).StatusCode);

      var report = await client.GetAsync("health");
      Assert.Contains("База даних недоступна", await report.Content.ReadAsStringAsync());
    }
    finally
    {
      factory.Store.DatabaseDown = false;
    }
  }

  [Fact]
  public async Task OpenApi_DescribesBothAuthSchemes()
  {
    var document = await factory.CreateClient().GetFromJsonAsync<JsonElement>("openapi/v1.json");
    var schemes = document.GetProperty("components").GetProperty("securitySchemes");

    Assert.Equal("bearer", schemes.GetProperty("Bearer").GetProperty("scheme").GetString());
    Assert.Equal("X-Api-Key", schemes.GetProperty("ApiKey").GetProperty("name").GetString());
  }
}

public class RateLimitTests(StrictLimitsApiFactory factory) : IClassFixture<StrictLimitsApiFactory>
{
  [Fact]
  public async Task Login_TooManyAttempts_429_WithRetryAfter()
  {
    var client = factory.CreateClient();
    var statuses = new List<HttpStatusCode>();

    for (var i = 0; i < 4; i++)
      statuses.Add((await client.PostAsJsonAsync("api/auth/login", new { login = "admin", password = "wrong-password1" })).StatusCode);

    var limited = await client.PostAsJsonAsync("api/auth/login", new { login = "admin", password = "wrong-password1" });

    Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    Assert.Contains("Забагато запитів", (await Api.Problem(limited)).GetProperty("title").GetString());
  }
}
