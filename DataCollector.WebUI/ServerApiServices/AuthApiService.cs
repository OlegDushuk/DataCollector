using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.ServerApiServices;

/// <summary>Вхід (api/auth) і власний пароль.</summary>
public class AuthApiService(HttpClient httpClient, ApiContext context) : ApiServiceBase(httpClient, context)
{
  /// <summary>Невірний логін або пароль, блокування - ApiException з поясненням від API.</summary>
  public Task<LoginResult> Login(string login, string password)
    => PostAsync<LoginResult>("api/auth/login", new { login, password });

  public Task ChangePassword(string currentPassword, string newPassword)
    => PostNoContentAsync("api/auth/change-password", new { currentPassword, newPassword });
}
