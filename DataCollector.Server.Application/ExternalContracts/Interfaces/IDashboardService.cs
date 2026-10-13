using DataCollector.Server.Application.ExternalContracts.Models;

namespace DataCollector.Server.Application.ExternalContracts.Interfaces;

/// <summary>Дашборди: збережені набори віджетів. Дані віджети беруть через звичайні ендпоінти записів.</summary>
public interface IDashboardService
{
  Task<List<DashboardListItemDto>> GetAll();
  Task<DashboardDto> Get(Guid id);
  Task<Guid> Create(SaveDashboardCommand command);
  Task Save(Guid id, SaveDashboardCommand command);
  Task Delete(Guid id);

  /// <summary>Копія дашборду з усіма віджетами; повертає Id копії.</summary>
  Task<Guid> Copy(Guid id);
}
