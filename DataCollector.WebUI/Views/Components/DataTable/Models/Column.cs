using DataCollector.WebUI.Views.Components.DataTable.Enums;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Components.DataTable.Models;

/// <summary>
/// Опис колонки. Оновлюється з <c>ColumnTemplate</c> при кожній зміні параметрів.
/// </summary>
public class Column<TItem>
{
  public RenderFragment<TItem> Template { get; set; } = _ => _ => { };
  public string? Key { get; set; }
  public string Title { get; set; } = string.Empty;
  public bool IsSortable { get; set; }
  public bool IsFilterable { get; set; }
  public DataTableFilterType FilterType { get; set; }

  /// <summary>Фіксована ширина (наприклад "120px"); без неї колонка розтягується.</summary>
  public string? Width { get; set; }

  public bool CanSort => IsSortable && !string.IsNullOrEmpty(Key);

  public string Style => Width is null ? string.Empty : $"flex: 0 0 {Width};";
}
