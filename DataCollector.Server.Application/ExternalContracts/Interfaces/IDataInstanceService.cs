namespace DataCollector.Server.Application.ExternalContracts.Interfaces;

public interface IDataInstanceService
{
  Task<Guid> CreateInstance();
}