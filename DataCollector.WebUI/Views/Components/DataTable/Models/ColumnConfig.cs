using DataCollector.WebUI.Views.Components.DataTable.Enums;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Components.DataTable.Models;

public class ColumnConfig(
  RenderFragment<RowData> template,
  DataTableColumnDataType dataType,
  string name = "")
{
  public RenderFragment<RowData> Template => template;
  public string Name => name;
  public DataTableColumnDataType DataType => dataType;
}