using DataCollector.Server.Domain.Entities;

namespace DataCollector.Server.Application.Interfaces.Repositories;

public interface IEntityConfigRepository
{
    Task Create(EntityConfig model);
    Task<EntityConfig?> GetById(Guid id);
    Task Update(EntityConfig model);
}