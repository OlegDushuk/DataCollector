using System.Text.Json;

namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>Рядок списку дашбордів.</summary>
public class DashboardListItemDto
{
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;
  public string? Description { get; set; }
  public int RefreshSeconds { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public int WidgetCount { get; set; }
}

public class DashboardDto
{
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;
  public string? Description { get; set; }
  public int RefreshSeconds { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public List<DashboardWidgetDto> Widgets { get; set; } = [];
}

/// <summary>
/// Віджет: тип (table, summary, chart, kpi), модель (null - не вибрана або видалена),
/// позиція в сітці на 12 колонок і налаштування - довільний JSON-об'єкт інтерфейсу.
/// </summary>
public class DashboardWidgetDto
{
  public Guid Id { get; set; }
  public Guid? ConfigId { get; set; }
  public string Type { get; set; } = null!;
  public string? Title { get; set; }
  public int X { get; set; }
  public int Y { get; set; }
  public int W { get; set; }
  public int H { get; set; }
  public JsonElement Settings { get; set; }
}

/// <summary>
/// Створення або повне збереження дашборду. Віджети - повний набір: відсутні в списку видаляються,
/// віджет без Id (або з новим Id) додається.
/// </summary>
public class SaveDashboardCommand
{
  public string Name { get; set; } = null!;
  public string? Description { get; set; }
  public int RefreshSeconds { get; set; }
  public List<SaveDashboardWidget> Widgets { get; set; } = [];
}

public class SaveDashboardWidget
{
  public Guid? Id { get; set; }
  public Guid? ConfigId { get; set; }
  public string Type { get; set; } = null!;
  public string? Title { get; set; }
  public int X { get; set; }
  public int Y { get; set; }
  public int W { get; set; }
  public int H { get; set; }
  public JsonElement? Settings { get; set; }
}
