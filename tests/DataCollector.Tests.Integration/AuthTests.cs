using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DataCollector.Tests.Integration;

/// <summary>Вхід, ролі, API-ключі, відкликання токенів.</summary>
public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
  [Fact]
  public async Task WithoutCredentials_401_ProblemDetails()
  {
    var response = await factory.CreateClient().GetAsync("api/data-configs");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Equal(401, (await Api.Problem(response)).GetProperty("status").GetInt32());
  }

  [Fact]
  public async Task WrongPassword_401()
  {
    var response = await factory.CreateClient().PostAsJsonAsync("api/auth/login", new { login = "admin", password = "wrong-password1" });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task InitialAdmin_CanSignIn_AndSeeHimself()
  {
    var client = await factory.SignIn();

    var me = await client.GetFromJsonAsync<JsonElement>("api/auth/me");

    Assert.Equal("admin", me.GetProperty("login").GetString());
    Assert.Equal("Admin", me.GetProperty("role").GetString());
  }

  [Fact]
  public async Task Viewer_CanRead_ButCannotWrite()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);
    var viewer = await factory.SignInAs("Viewer");

    Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"api/data-configs/{model}/records")).StatusCode);

    var create = await viewer.PostAsJsonAsync($"api/data-configs/{model}/records", new { values = new { number = "ORD-1" } });
    Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
  }

  [Fact]
  public async Task Editor_CannotManageModelsOrUsers()
  {
    var editor = await factory.SignInAs("Editor");

    Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsJsonAsync("api/data-configs", new { name = "X", key = "x" })).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("api/users")).StatusCode);
  }

  [Fact]
  public async Task ApiKey_WritesData_UntilRevoked()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);
    var created = await (await admin.PostAsJsonAsync("api/api-keys", new { name = "Каса магазину", role = "Editor" }))
      .Content.ReadFromJsonAsync<JsonElement>();
    var plainKey = created.GetProperty("plainKey").GetString()!;
    var keyId = created.GetProperty("key").GetProperty("id").GetGuid();

    var integration = factory.CreateClient();
    integration.DefaultRequestHeaders.Add("X-Api-Key", plainKey);
    await Api.CreateRecord(integration, model, new { number = "POS-1", price = 99.9 });

    (await admin.PostAsync($"api/api-keys/{keyId}/revoke", null)).EnsureSuccessStatusCode();

    var afterRevoke = await integration.GetAsync($"api/data-configs/{model}/records");
    Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
  }

  [Fact]
  public async Task PasswordChange_InvalidatesExistingTokens()
  {
    var admin = await factory.SignIn();
    var login = "user_" + Guid.NewGuid().ToString("N")[..8];
    (await admin.PostAsJsonAsync("api/users", new { login, role = "Editor", password = "Passw0rd123" })).EnsureSuccessStatusCode();
    var oldSession = await factory.SignIn(login, "Passw0rd123");

    var change = await oldSession.PostAsJsonAsync("api/auth/change-password", new { currentPassword = "Passw0rd123", newPassword = "NewPassw0rd456" });
    Assert.True(change.IsSuccessStatusCode);

    Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("api/auth/me")).StatusCode);
    var newSession = await factory.SignIn(login, "NewPassw0rd456");
    Assert.Equal(HttpStatusCode.OK, (await newSession.GetAsync("api/auth/me")).StatusCode);
  }
}
