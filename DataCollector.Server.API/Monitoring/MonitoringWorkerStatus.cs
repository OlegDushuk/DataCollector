namespace DataCollector.Server.API.Monitoring;

/// <summary>
/// Стан фонового сервісу моніторингу для health check: коли завершився останній прохід і чи був він успішним.
/// Сервіс, що «завис» або впав, перестає оновлювати час - перевірка стану це помітить.
/// </summary>
public sealed class MonitoringWorkerStatus
{
  private long _lastPassTicks;
  private volatile string? _lastError;

  public DateTime StartedAt { get; } = DateTime.UtcNow;

  public DateTime? LastPassAt
  {
    get
    {
      var ticks = Interlocked.Read(ref _lastPassTicks);
      return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
    }
  }

  /// <summary>Помилка останнього проходу (null - прохід успішний).</summary>
  public string? LastError => _lastError;

  public void PassCompleted(string? error)
  {
    _lastError = error;
    Interlocked.Exchange(ref _lastPassTicks, DateTime.UtcNow.Ticks);
  }
}
