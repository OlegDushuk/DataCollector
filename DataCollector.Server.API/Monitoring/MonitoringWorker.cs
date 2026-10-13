using DataCollector.Server.Application.ExternalContracts.Interfaces;
using Microsoft.Extensions.Options;

namespace DataCollector.Server.API.Monitoring;

/// <summary>
/// Фоновий сервіс моніторингу: кожні <see cref="MonitoringOptions.TickSeconds"/> секунд перевіряє правила,
/// яким настав час (у кожного правила свій інтервал), і надсилає сповіщення, що очікують доставки.
/// </summary>
/// <remarks>
/// Кожен прохід - окрема область DI (сервіси transient, з'єднання з БД - на кожен запит). Помилка проходу
/// пишеться в журнал і не зупиняє сервіс: наступний прохід - за розкладом.
/// </remarks>
public sealed class MonitoringWorker(
  IServiceScopeFactory scopes,
  IOptions<MonitoringOptions> options,
  ILogger<MonitoringWorker> logger
) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!options.Value.Enabled)
    {
      logger.LogInformation("Моніторинг вимкнено (Monitoring:Enabled = false)");
      return;
    }

    var tick = TimeSpan.FromSeconds(Math.Clamp(options.Value.TickSeconds, 5, 300));
    logger.LogInformation("Моніторинг запущено: перевірка правил кожні {Seconds} с", tick.TotalSeconds);

    // Невелика пауза після старту: API встигає підняти з'єднання з БД, а розробник - побачити журнал запуску.
    try
    {
      await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
    }
    catch (OperationCanceledException)
    {
      return;
    }

    using var timer = new PeriodicTimer(tick);

    do
    {
      await RunOnce(stoppingToken);
    }
    while (await WaitNext(timer, stoppingToken));
  }

  private static async Task<bool> WaitNext(PeriodicTimer timer, CancellationToken stoppingToken)
  {
    try
    {
      return await timer.WaitForNextTickAsync(stoppingToken);
    }
    catch (OperationCanceledException)
    {
      return false;
    }
  }

  private async Task RunOnce(CancellationToken stoppingToken)
  {
    using var scope = scopes.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<IMonitoringRunner>();

    try
    {
      var checkedCount = await runner.RunDue(stoppingToken);
      if (checkedCount > 0)
        logger.LogDebug("Моніторинг: перевірено правил - {Count}", checkedCount);
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      return;
    }
    catch (Exception e)
    {
      logger.LogError(e, "Моніторинг: помилка перевірки правил");
    }

    try
    {
      await runner.DeliverPending(stoppingToken);
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
    }
    catch (Exception e)
    {
      logger.LogError(e, "Моніторинг: помилка доставки сповіщень");
    }
  }
}
