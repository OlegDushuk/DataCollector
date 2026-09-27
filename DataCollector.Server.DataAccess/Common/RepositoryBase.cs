using System.Data;
using Dapper;
using DataCollector.Server.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Common;

public abstract class RepositoryBase
{
  // Коди помилок, які кидають збережені процедури через THROW.
  private const int NotFoundErrorNumber = 50404;
  private const int ConflictErrorNumber = 50409;

  // Порушення UNIQUE-обмежень / індексів.
  private const int UniqueConstraintErrorNumber = 2627;
  private const int UniqueIndexErrorNumber = 2601;

  private readonly string _connectionString;
  private readonly string _tableName;
  
  protected RepositoryBase(IConfiguration config, string tableName)
  {
    _connectionString = config.GetConnectionString("DefaultConnection")
                        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured");
    
    _tableName = tableName;
  }

  private SqlConnection GetConnection()
  {
    return new SqlConnection(_connectionString);
  }

  private string GetProcedureName(string procedureName)
  {
    return $"sp_{_tableName}_{procedureName}";
  }

  protected Task ExecuteAsync(string procedureName, object? param = null)
  {
    return Run(async connection =>
    {
      await connection.ExecuteAsync(
        sql: GetProcedureName(procedureName),
        param: param,
        commandType: CommandType.StoredProcedure);
      
      return true;
    });
  }
  
  protected Task<TEntity?> QueryFirstOrDefaultAsync<TEntity>(string procedureName, object? param = null)
  {
    return Run(connection => connection.QueryFirstOrDefaultAsync<TEntity>(
      sql: GetProcedureName(procedureName),
      param: param,
      commandType: CommandType.StoredProcedure));
  }

  protected Task<List<TEntity>> QueryListAsync<TEntity>(string procedureName, object? param = null)
  {
    return Run(async connection =>
    {
      var result = await connection.QueryAsync<TEntity>(
        sql: GetProcedureName(procedureName),
        param: param,
        commandType: CommandType.StoredProcedure);

      return result.ToList();
    });
  }

  /// <summary>
  /// Для процедур, що повертають кілька наборів результатів.
  /// </summary>
  protected Task<TResult> QueryMultipleAsync<TResult>(
    string procedureName,
    object? param,
    Func<SqlMapper.GridReader, Task<TResult>> read)
  {
    return Run(async connection =>
    {
      using var grid = await connection.QueryMultipleAsync(
        sql: GetProcedureName(procedureName),
        param: param,
        commandType: CommandType.StoredProcedure);

      return await read(grid);
    });
  }

  private async Task<TResult> Run<TResult>(Func<SqlConnection, Task<TResult>> action)
  {
    await using var connection = GetConnection();

    try
    {
      return await action(connection);
    }
    catch (SqlException e) when (e.Number == NotFoundErrorNumber)
    {
      throw new NotFoundException(e.Message);
    }
    catch (SqlException e) when (e.Number == ConflictErrorNumber)
    {
      throw new ConflictException(e.Message);
    }
    catch (SqlException e) when (e.Number is UniqueConstraintErrorNumber or UniqueIndexErrorNumber)
    {
      throw new ConflictException("Запис з такими даними вже існує");
    }
  }

  /// <summary>
  /// SQL Server не зберігає DateTimeKind, а ми всюди пишемо UTC.
  /// </summary>
  protected static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
  protected static DateTime? AsUtc(DateTime? value) => value.HasValue ? AsUtc(value.Value) : null;
}
