using DataCollector.WebUI.Views.Components.DataTable.Models;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Components.DataTable;

public partial class DataTable<TItem> : ComponentBase
{
  [Parameter] public RenderFragment? RowTemplate { get; set; }
  
  [Parameter] public List<TItem> Items { get; set; } = [];
  [Parameter] public int TotalCount { get; set; }
  [Parameter] public List<int> PageSizes { get; set; } = [5, 10, 20];
  [Parameter] public int StartPageSize { get; set; } = 10;
  
  [Parameter] public EventCallback<ReloadEventArgs> OnReload { get; set; }
  
  private readonly List<Column<TItem>> _columns = [];
  private int _pageSize;
  private int _pageNumber = 1;
  private string? _sortedColumnKey;
  private bool _isSortDescending;

  protected override void OnInitialized()
  {
    _pageSize = StartPageSize;
  }

  public void InitializeColumn(Column<TItem> config)
  {
    _columns.Add(config);
    StateHasChanged();
  }

  private void OnSortClick(Column<TItem> column)
  {
    if (!column.IsSortable)
      return;
    
    _isSortDescending = _sortedColumnKey != column.Key || !_isSortDescending;
    _sortedColumnKey = column.Key;
    
    InvokeReload();
  }

  private void OnPageNumberClick(int pageNumber)
  {
    _pageNumber = pageNumber;
    
    InvokeReload();
  }
  
  private void OnPageSizeClick(int pageSize)
  {
    _pageSize = pageSize;
    
    InvokeReload();
  }
  
  private void InvokeReload()
  {
    var args = new ReloadEventArgs
    {
      PageSize = _pageSize,
      PageNumber = _pageNumber,
      SortColumnKey = _sortedColumnKey,
      SortIsDesk = _isSortDescending
    };
    
    OnReload.InvokeAsync(args);
  }
}