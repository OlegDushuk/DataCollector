using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Application.Security;
using DataCollector.Server.Domain.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DataCollector.Server.Application.Services;

public class UserService(
  IUserRepository users,
  IPasswordHasher passwordHasher,
  IMemoryCache cache,
  ILogger<UserService> logger
) : IUserService
{
  public async Task<List<UserDto>> GetAll()
  {
    var now = DateTime.UtcNow;
    return (await users.GetAll())
      .OrderBy(u => u.Login, StringComparer.OrdinalIgnoreCase)
      .Select(u => u.ToDto(now))
      .ToList();
  }

  public async Task<Guid> Create(CreateUserCommand command)
  {
    var role = UserRoles.Parse(command.Role);
    var login = User.NormalizeLogin(command.Login);
    PasswordPolicy.Validate(command.Password);

    if (await users.GetByLogin(login) != null)
      throw new ConflictException($"Користувач з логіном '{login}' уже існує");

    var user = User.Create(login, command.DisplayName, role, passwordHasher.Hash(command.Password));
    await users.Create(user);

    logger.LogInformation("Створено користувача {Login} (роль {Role})", user.Login, user.Role);
    return user.Id;
  }

  public async Task Update(Guid id, UpdateUserCommand command, Guid actingUserId)
  {
    var role = UserRoles.Parse(command.Role);
    var all = await users.GetAll();
    var user = all.FirstOrDefault(u => u.Id == id) ?? throw new NotFoundException("Користувач не знайдений");

    var losesAdmin = user is { Role: UserRole.Admin, IsActive: true } && (role != UserRole.Admin || !command.IsActive);

    if (losesAdmin && id == actingUserId)
      throw new ConflictException("Не можна зняти з себе права адміністратора або вимкнути власний обліковий запис");

    // Без жодного активного адміністратора ніхто не зміг би керувати моделями й користувачами.
    if (losesAdmin && !all.Any(u => u.Id != id && u is { Role: UserRole.Admin, IsActive: true }))
      throw new ConflictException("Це останній активний адміністратор - спершу призначте іншого");

    var before = (user.Role, user.IsActive);
    user.Change(command.DisplayName, role, command.IsActive);
    await users.Update(user);
    AuthService.InvalidateSession(cache, user.Id);

    if (before != (user.Role, user.IsActive))
      logger.LogInformation("Користувача {Login} змінено: роль {OldRole} -> {Role}, доступ {OldActive} -> {Active}",
        user.Login, before.Role, user.Role, before.IsActive, user.IsActive);
    else
      logger.LogInformation("Змінено профіль користувача {Login}", user.Login);
  }

  public async Task SetPassword(Guid id, SetPasswordCommand command)
  {
    var user = await users.GetById(id) ?? throw new NotFoundException("Користувач не знайдений");

    PasswordPolicy.Validate(command.Password);
    user.SetPassword(passwordHasher.Hash(command.Password));
    await users.Update(user);
    AuthService.InvalidateSession(cache, user.Id);

    logger.LogInformation("Адміністратор встановив новий пароль користувачу {Login}", user.Login);
  }
}
