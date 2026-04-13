using DataCollector.WebUI.Views.Components.DataTable.Models;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Components.DataTable;

public partial class DataTable : ComponentBase
{
  [Parameter] public RenderFragment? RowTemplate { get; set; }

  [Parameter] public List<RowData> Items { get; set; } = [];
  [Parameter] public int TotalCount { get; set; }
  
  [Parameter] public EventCallback<string> OnSort { get; set; }
  
  private readonly List<ColumnConfig> _columnConfigs = [];

 public void InitializeColumn(ColumnConfig config)
  {
    _columnConfigs.Add(config);
    StateHasChanged();
  }
}