using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Common;
using DataCollector.Server.DataAccess.Rows;
using DataCollector.Server.Domain.Users;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

public class ApiKeyRepository(IConfiguration config)
  : RepositoryBase(config, "ApiKeys"), IApiKeyRepository
{
  public async Task<List<ApiKeyRecord>> GetAll()
    => (await QueryListAsync<ApiKeyRow>("GetAll")).Select(r => new ApiKeyRecord(ToDomain(r), r.CreatedByName)).ToList();

  public async Task<ApiKey?> GetById(Guid id)
    => await QueryFirstOrDefaultAsync<ApiKeyRow>("GetById", new { Id = id }) is { } row ? ToDomain(row) : null;

  public async Task<ApiKey?> GetByHash(byte[] keyHash)
    => await QueryFirstOrDefaultAsync<ApiKeyRow>("GetByHash", new { KeyHash = keyHash }) is { } row ? ToDomain(row) : null;

  public Task Create(ApiKey key) => ExecuteAsync("Create", new
  {
    key.Id,
    key.Name,
    key.Prefix,
    key.KeyHash,
    Role = (byte)key.Role,
    key.CreatedAt,
    key.CreatedByUserId
  });

  public Task Revoke(Guid id, DateTime revokedAt) => ExecuteAsync("Revoke", new { Id = id, RevokedAt = revokedAt });

  public Task MarkUsed(Guid id, DateTime usedAt) => ExecuteAsync("MarkUsed", new { Id = id, UsedAt = usedAt });

  private static ApiKey ToDomain(ApiKeyRow row) => ApiKey.Restore(
    row.Id, row.Name, row.Prefix, row.KeyHash, (UserRole)row.Role, AsUtc(row.CreatedAt), row.CreatedByUserId,
    AsUtc(row.LastUsedAt), AsUtc(row.RevokedAt));
}
