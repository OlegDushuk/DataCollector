using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Common;
using DataCollector.Server.DataAccess.Rows;
using DataCollector.Server.Domain.Users;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

public class UserRepository(IConfiguration config)
  : RepositoryBase(config, "Users"), IUserRepository
{
  public async Task<List<User>> GetAll()
    => (await QueryListAsync<UserRow>("GetAll")).Select(ToDomain).ToList();

  public async Task<User?> GetById(Guid id)
    => await QueryFirstOrDefaultAsync<UserRow>("GetById", new { Id = id }) is { } row ? ToDomain(row) : null;

  public async Task<User?> GetByLogin(string login)
    => await QueryFirstOrDefaultAsync<UserRow>("GetByLogin", new { Login = login.Trim() }) is { } row ? ToDomain(row) : null;

  public Task Create(User user) => ExecuteAsync("Create", new
  {
    user.Id,
    user.Login,
    user.DisplayName,
    Role = (byte)user.Role,
    user.PasswordHash,
    user.SecurityStamp,
    user.IsActive,
    user.CreatedAt
  });

  public Task Update(User user) => ExecuteAsync("Update", new
  {
    user.Id,
    user.DisplayName,
    Role = (byte)user.Role,
    user.PasswordHash,
    user.SecurityStamp,
    user.IsActive,
    user.FailedLoginCount,
    user.LockoutEnd,
    user.UpdatedAt,
    user.LastLoginAt
  });

  private static User ToDomain(UserRow row) => User.Restore(
    row.Id, row.Login, row.DisplayName, (UserRole)row.Role, row.PasswordHash, row.SecurityStamp, row.IsActive,
    row.FailedLoginCount, AsUtc(row.LockoutEnd), AsUtc(row.CreatedAt), AsUtc(row.UpdatedAt), AsUtc(row.LastLoginAt));
}
