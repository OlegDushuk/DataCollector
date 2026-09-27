using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Common;

public abstract class RepositoryBase
{
  private readonly string _connectionString;
  private readonly string _tableName;
  
  protected RepositoryBase(IConfiguration config, string tableName)
  {
    _connectionString = config.GetConnectionString("DefaultConnection")
                        ?? throw new NullReferenceException(nameof(config));
    
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

  protected async Task ExecuteAsync(string procedureName, object param)
  {
    await using var connection = GetConnection();
    
    await connection.ExecuteAsync(
      sql: GetProcedureName(procedureName),
      param: param,
      commandType: CommandType.StoredProcedure);
  }
  
  protected async Task<TEntity?> QueryAsync<TEntity>(string procedureName, object param)
  {
    await using var connection = GetConnection();
    
    return await connection.QueryFirstOrDefaultAsync(
      sql: GetProcedureName(procedureName),
      param: param,
      commandType: CommandType.StoredProcedure);
  }
}