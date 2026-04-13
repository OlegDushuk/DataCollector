using DataCollector.Business.DTOs;
using DataCollector.Business.Interfaces.Repositories;
using DataCollector.Domain.Entities;

namespace DataCollector.Business.Services;

public class DataConfigService(
    IDataConfigRepository dataConfigRepository
    )
{
    public async Task CreateDataConfig(CreateDataConfigDto dto)
    {
        var config = new DataConfig
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Name = dto.Name,
        };
        
        await dataConfigRepository.CreateDataConfig(config);
    }
}