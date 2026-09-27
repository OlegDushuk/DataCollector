using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Domain.Entities;

namespace DataCollector.Server.Application.Interfaces.Repositories;

public interface IEntityConfigRepository
{
    Task<List<EntityConfigListItemDto>> GetAll();
    Task<EntityConfig?> GetById(Guid id);
    Task<EntityConfig?> GetByKey(string key);

    /// <summary>Створює модель разом з полями.</summary>
    Task Create(EntityConfig model);
    Task Update(EntityConfig model);
    Task Delete(Guid id);
}
