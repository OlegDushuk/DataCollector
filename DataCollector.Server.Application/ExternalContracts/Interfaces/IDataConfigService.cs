using DataCollector.Server.Application.ExternalContracts.Models;

namespace DataCollector.Server.Application.ExternalContracts.Interfaces;

public interface IDataConfigService
{
  Task CreateDataConfig(CreateEntityConfigCommand command);
  Task EditDataConfig(EditEntityConfigCommand command);
}