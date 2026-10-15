using System.Net.Http.Headers;
using System.Net.Http.Json;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Tests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataCollector.Tests.Integration;

/// <summary>
/// API у пам'яті процесу тестів (TestServer): справжній конвеєр ASP.NET Core - автентифікація, ролі, ліміти,
/// контролери, сервіси, Problem Details, - але сховище в пам'яті замість SQL Server.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
  public const string AdminLogin = "admin";
  public const string AdminPassword = "Admin12345";

  public InMemoryStore Store { get; } = new();

  /// <summary>Спроб входу за хвилину з однієї адреси (у тестах лімітів - менше).</summary>
  protected virtual int LoginAttemptsPerMinute => 1000;

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Development");
    builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(tests);Database=none");
    builder.UseSetting("Auth:InitialAdmin:Login", AdminLogin);
    builder.UseSetting("Auth:InitialAdmin:Password", AdminPassword);
    builder.UseSetting("Monitoring:Enabled", "false");
    builder.UseSetting("RateLimiting:LoginAttemptsPerMinute", LoginAttemptsPerMinute.ToString());
    builder.UseSetting("RateLimiting:AnonymousRequestsPerMinute", "1000");

    builder.ConfigureServices(services =>
    {
      services.RemoveAll<IEntityConfigRepository>();
      services.RemoveAll<IEntityPropertyConfigRepository>();
      services.RemoveAll<IEntityInstanceRepository>();
      services.RemoveAll<IDashboardRepository>();
      services.RemoveAll<IMonitoringRuleRepository>();
      services.RemoveAll<INotificationRepository>();
      services.RemoveAll<IUserRepository>();
      services.RemoveAll<IApiKeyRepository>();
      services.RemoveAll<IDatabaseHealth>();

      services.AddSingleton(Store);
      services.AddSingleton<IEntityConfigRepository, InMemoryEntityConfigRepository>();
      services.AddSingleton<IEntityPropertyConfigRepository, InMemoryEntityPropertyConfigRepository>();
      services.AddSingleton<IEntityInstanceRepository, InMemoryEntityInstanceRepository>();
      services.AddSingleton<IDashboardRepository, InMemoryDashboardRepository>();
      services.AddSingleton<IMonitoringRuleRepository, InMemoryMonitoringRuleRepository>();
      services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
      services.AddSingleton<IUserRepository, InMemoryUserRepository>();
      services.AddSingleton<IApiKeyRepository, InMemoryApiKeyRepository>();
      services.AddSingleton<IDatabaseHealth, InMemoryDatabaseHealth>();
    });
  }

  /// <summary>Клієнт з токеном користувача.</summary>
  public async Task<HttpClient> SignIn(string login = AdminLogin, string password = AdminPassword)
  {
    var client = CreateClient();
    var response = await client.PostAsJsonAsync("api/auth/login", new { login, password });
    response.EnsureSuccessStatusCode();

    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result!.AccessToken);
    return client;
  }

  /// <summary>Новий користувач з роллю (створює адміністратор) і клієнт від його імені.</summary>
  public async Task<HttpClient> SignInAs(string role)
  {
    var admin = await SignIn();
    var login = role.ToLowerInvariant() + "_" + Guid.NewGuid().ToString("N")[..8];
    (await admin.PostAsJsonAsync("api/users", new { login, role, password = "Passw0rd123" })).EnsureSuccessStatusCode();
    return await SignIn(login, "Passw0rd123");
  }

  public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt);
}

/// <summary>Окремий екземпляр API з малим лімітом входу - щоб перевірити 429.</summary>
public sealed class StrictLimitsApiFactory : ApiFactory
{
  protected override int LoginAttemptsPerMinute => 3;
}
