using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.ServerApiServices;

/// <summary>Користувачі (api/users) і API-ключі (api/api-keys) - для адміністратора.</summary>
public class UsersApiService(HttpClient httpClient, ApiContext context) : ApiServiceBase(httpClient, context)
{
  public Task<List<UserItem>> GetUsers() => GetAsync<List<UserItem>>("api/users");

  public async Task<Guid> CreateUser(UserForm form)
  {
    var result = await PostAsync<IdResponse>("api/users", new
    {
      login = form.Login,
      displayName = form.DisplayName,
      role = form.Role,
      password = form.Password
    });
    return result.Id;
  }

  public Task UpdateUser(UserForm form)
    => PutAsync($"api/users/{form.Id}", new { displayName = form.DisplayName, role = form.Role, isActive = form.IsActive });

  public Task SetPassword(Guid id, string password)
    => PostNoContentAsync($"api/users/{id}/password", new { password });

  public Task<List<ApiKeyItem>> GetApiKeys() => GetAsync<List<ApiKeyItem>>("api/api-keys");

  public Task<CreatedApiKey> CreateApiKey(string name, string role)
    => PostAsync<CreatedApiKey>("api/api-keys", new { name, role });

  public Task RevokeApiKey(Guid id) => PostEmptyAsync($"api/api-keys/{id}/revoke");

  private class IdResponse
  {
    public Guid Id { get; set; }
  }
}
