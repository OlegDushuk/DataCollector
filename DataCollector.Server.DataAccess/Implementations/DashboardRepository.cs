using System.Data;
using Dapper;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.DataAccess.Common;
using DataCollector.Server.DataAccess.Rows;
using DataCollector.Server.Domain.Dashboards;
using Microsoft.Extensions.Configuration;

namespace DataCollector.Server.DataAccess.Implementations;

public class DashboardRepository(IConfiguration config)
  : RepositoryBase(config, "Dashboards"), IDashboardRepository
{
  public async Task<List<DashboardListItemDto>> GetAll()
  {
    var items = await QueryListAsync<DashboardListItemDto>("GetAll");
    items.ForEach(i =>
    {
      i.CreatedAt = AsUtc(i.CreatedAt);
      i.UpdatedAt = AsUtc(i.UpdatedAt);
    });

    return items;
  }

  public Task<Dashboard?> GetById(Guid id)
  {
    return QueryMultipleAsync<Dashboard?>("GetById", new { Id = id }, async grid =>
    {
      var row = await grid.ReadFirstOrDefaultAsync<DashboardRow>();
      var widgetRows = await grid.ReadAsync<DashboardWidgetRow>();

      if (row is null)
        return null;

      // Віджети відновлюються через ті самі перевірки, що й при збереженні.
      // Невідомий тип (запис зроблено новішою версією) не ламає дашборд - такий віджет пропускається.
      var widgets = widgetRows
        .Where(w => DashboardWidgetTypes.Parse(w.Type) != null)
        .Select(w => new DashboardWidget(
          w.Id, w.ConfigId, DashboardWidgetTypes.Parse(w.Type)!.Value, w.Title, w.X, w.Y, w.W, w.H, w.Settings));

      return new Dashboard(
        row.Name, row.Description, row.RefreshSeconds, widgets, row.Id, AsUtc(row.CreatedAt), AsUtc(row.UpdatedAt),
        row.Version);
    });
  }

  public Task Create(Dashboard dashboard)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", dashboard.Id);
    parameters.Add("Name", dashboard.Name);
    parameters.Add("Description", dashboard.Description);
    parameters.Add("RefreshSeconds", dashboard.RefreshSeconds);
    parameters.Add("CreatedAt", dashboard.CreatedAt);
    parameters.Add("Widgets", ToWidgetList(dashboard.Widgets));

    return ExecuteAsync("Create", parameters);
  }

  public Task<long> Update(Dashboard dashboard)
  {
    var parameters = new DynamicParameters();
    parameters.Add("Id", dashboard.Id);
    parameters.Add("Name", dashboard.Name);
    parameters.Add("Description", dashboard.Description);
    parameters.Add("RefreshSeconds", dashboard.RefreshSeconds);
    parameters.Add("UpdatedAt", dashboard.UpdatedAt ?? DateTime.UtcNow);
    parameters.Add("ExpectedVersion", dashboard.Version);
    parameters.Add("Widgets", ToWidgetList(dashboard.Widgets));

    return QueryFirstOrDefaultAsync<long>("Update", parameters);
  }

  public Task Delete(Guid id)
  {
    return ExecuteAsync("Delete", new { Id = id });
  }

  /// <summary>Віджети для dbo.DashboardWidgetList; порядок колонок має збігатися з типом.</summary>
  private static SqlMapper.ICustomQueryParameter ToWidgetList(IEnumerable<DashboardWidget> widgets)
  {
    var table = new DataTable();

    table.Columns.Add("Id", typeof(Guid));
    table.Columns.Add("ConfigId", typeof(Guid));
    table.Columns.Add("Type", typeof(string));
    table.Columns.Add("Title", typeof(string));
    table.Columns.Add("X", typeof(int));
    table.Columns.Add("Y", typeof(int));
    table.Columns.Add("W", typeof(int));
    table.Columns.Add("H", typeof(int));
    table.Columns.Add("Settings", typeof(string));

    foreach (var w in widgets)
    {
      table.Rows.Add(
        w.Id,
        w.ConfigId ?? (object)DBNull.Value,
        w.Type.Code(),
        w.Title ?? (object)DBNull.Value,
        w.X, w.Y, w.W, w.H,
        w.Settings);
    }

    return table.AsTableValuedParameter("dbo.DashboardWidgetList");
  }
}
