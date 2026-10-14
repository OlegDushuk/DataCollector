using DataCollector.Server.Application.ExternalContracts.Interfaces;
using Microsoft.Extensions.Options;

namespace DataCollector.Server.API.Auth;

/// <summary>
/// При запуску API: якщо в базі ще немає користувачів, створює першого адміністратора з налаштувань
/// Auth:InitialAdmin. Без цього в систему нікому було б увійти.
/// </summary>
public sealed class AuthBootstrapper(
  IServiceProvider services,
  IOptions<AuthOptions> options,
  ILogger<AuthBootstrapper> logger
) : IHostedService
{
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    try
    {
      using var scope = services.CreateScope();
      var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
      var admin = options.Value.InitialAdmin;

      await auth.EnsureInitialAdmin(admin.Login, admin.Password);
    }
    catch (Exception e)
    {
      // База ще недоступна або не оновлена (publish-db) - API все одно запускається, помилка видна в журналі.
      logger.LogError(e, "Не вдалося перевірити наявність користувачів - перевірте підключення до БД і виконайте publish-db.ps1");
    }
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
