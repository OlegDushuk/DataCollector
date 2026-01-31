using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Shared.Tables;

public partial class DataTable<TItem> : ComponentBase
{
  [Parameter] public string? CssClass { get; set; } = string.Empty;
  [Parameter] public string? RowCssClass { get; set; } = string.Empty;
  [Parameter] public string? ColumnCssClass { get; set; } = string.Empty;
  [Parameter] public RenderFragment? RowTemplate { get; set; }

  [Parameter] public List<TItem> Items { get; set; } = [];
  
  private readonly List<ColumnConfig> _columnConfigs = [];
  private DataTableAccessor _dataTableAccessor = null!;

  protected override void OnInitialized()
  {
    _dataTableAccessor = new DataTableAccessor(InitializeColumn);
  }

  private void InitializeColumn(ColumnConfig config)
  {
    _columnConfigs.Add(config);
    StateHasChanged();
  }
}