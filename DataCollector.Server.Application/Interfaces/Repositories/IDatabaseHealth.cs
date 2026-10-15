namespace DataCollector.Server.Application.Interfaces.Repositories;

/// <summary>Перевірка доступності бази даних для health checks.</summary>
public interface IDatabaseHealth
{
  /// <summary>Найпростіший запит до БД; виняток - база недоступна.</summary>
  Task Ping(CancellationToken cancellationToken = default);
}
