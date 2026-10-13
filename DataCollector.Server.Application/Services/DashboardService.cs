using System.Text.Json;
using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Domain.Dashboards;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Application.Services;

public class DashboardService(IDashboardRepository dashboardRepository) : IDashboardService
{
  public Task<List<DashboardListItemDto>> GetAll()
  {
    return dashboardRepository.GetAll();
  }

  public async Task<DashboardDto> Get(Guid id)
  {
    var dashboard = await Load(id);
    return ToDto(dashboard);
  }

  public async Task<Guid> Create(SaveDashboardCommand command)
  {
    var dashboard = new Dashboard(command.Name, command.Description, command.RefreshSeconds, Widgets(command));
    await dashboardRepository.Create(dashboard);
    return dashboard.Id;
  }

  public async Task Save(Guid id, SaveDashboardCommand command)
  {
    var dashboard = await Load(id);
    dashboard.Change(command.Name, command.Description, command.RefreshSeconds, Widgets(command));
    await dashboardRepository.Update(dashboard);
  }

  public Task Delete(Guid id)
  {
    return dashboardRepository.Delete(id);
  }

  public async Task<Guid> Copy(Guid id)
  {
    var copy = (await Load(id)).Copy();
    await dashboardRepository.Create(copy);
    return copy.Id;
  }

  private async Task<Dashboard> Load(Guid id)
  {
    return await dashboardRepository.GetById(id) ?? throw new NotFoundException("Дашборд не знайдений");
  }

  private static IEnumerable<DashboardWidget> Widgets(SaveDashboardCommand command)
  {
    return (command.Widgets ?? []).Select(w => new DashboardWidget(
      w.Id,
      w.ConfigId,
      DashboardWidgetTypes.Parse(w.Type) ?? throw new DomainValidationException($"Невідомий тип віджета '{w.Type}'"),
      w.Title,
      w.X, w.Y, w.W, w.H,
      w.Settings is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } settings
        ? settings.GetRawText()
        : null));
  }

  private static DashboardDto ToDto(Dashboard dashboard) => new()
  {
    Id = dashboard.Id,
    Name = dashboard.Name,
    Description = dashboard.Description,
    RefreshSeconds = dashboard.RefreshSeconds,
    CreatedAt = dashboard.CreatedAt,
    UpdatedAt = dashboard.UpdatedAt,
    Widgets = dashboard.Widgets.Select(w => new DashboardWidgetDto
    {
      Id = w.Id,
      ConfigId = w.ConfigId,
      Type = w.Type.Code(),
      Title = w.Title,
      X = w.X,
      Y = w.Y,
      W = w.W,
      H = w.H,
      Settings = ParseSettings(w.Settings)
    }).ToList()
  };

  private static JsonElement ParseSettings(string settings)
  {
    using var document = JsonDocument.Parse(settings);
    return document.RootElement.Clone();
  }
}
