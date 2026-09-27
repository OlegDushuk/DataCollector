using DataCollector.Server.Domain.Entities;

namespace DataCollector.Server.Application.Interfaces.Repositories;

public interface IEntityPropertyConfigRepository
{
  Task CreateManyAsync(EntityConfig entityConfig);
}