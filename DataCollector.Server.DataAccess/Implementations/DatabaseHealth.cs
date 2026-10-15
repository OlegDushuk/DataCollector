using Dapper;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Common;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

/// <summary>
/// «SELECT 1»: відкриває з'єднання (з пулу) і виконує запит без звернення до таблиць - тож працює й під обліковим
/// записом застосунку з мінімальними правами і не навантажує базу.
/// </summary>
public class DatabaseHealth(IConfiguration config) : RepositoryBase(config, "Health"), IDatabaseHealth
{
  public Task Ping(CancellationToken cancellationToken = default)
    => QueryRowsSqlAsync("SELECT 1 AS [Ok];", new DynamicParameters(), cancellationToken);
}
