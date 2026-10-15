using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Domain.Dashboards;

namespace DataCollector.Server.Application.Interfaces.Repositories;

public interface IDashboardRepository
{
  Task<List<DashboardListItemDto>> GetAll();
  Task<Dashboard?> GetById(Guid id);

  /// <summary>Створює дашборд разом з віджетами.</summary>
  Task Create(Dashboard dashboard);

  /// <summary>
  /// Зберігає дашборд і приводить його віджети в БД до <see cref="Dashboard.Widgets"/>, якщо версія в сховищі
  /// досі <see cref="Dashboard.Version"/> (інакше - конфлікт). Повертає нову версію.
  /// </summary>
  Task<long> Update(Dashboard dashboard);

  Task Delete(Guid id);
}
