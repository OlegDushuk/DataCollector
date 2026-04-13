using DataCollector.Domain.Entities;

namespace DataCollector.Business.Interfaces.Repositories;

public interface IDataConfigRepository
{
    Task CreateDataConfig(DataConfig model);
}