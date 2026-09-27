using DataCollector.WebUI.Views.Components.DataTable.Enums;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Components.DataTable.Models;

public class Column<TItem>(
  RenderFragment<TItem> template,
  string key,
  string title,
  bool isSortable,
  bool isFilterable,
  DataTableFilterType filterType)
{
  public RenderFragment<TItem> Template => template;
  public string Key => key;
  public string Title => title;
  public bool IsSortable => isSortable;
  public bool IsFilterable => isFilterable;
  public DataTableFilterType FilterType => filterType;
}