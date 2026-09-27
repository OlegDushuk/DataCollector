using DataCollector.WebUI.Views.Components.DataTable.Models;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Components.DataTable;

/// <summary>
/// Таблиця з сортуванням і пагінацією. Дані не зберігає: при зміні сторінки/сортування
/// викликає <see cref="OnReload"/>, а сторінка-власник завантажує потрібний шматок і передає його в <see cref="Items"/>.
/// Перше завантаження відбувається автоматично після першого рендеру.
/// </summary>
public partial class DataTable<TItem> : ComponentBase
{
  [Parameter] public RenderFragment? RowTemplate { get; set; }
  [Parameter] public RenderFragment? ToolbarLeft { get; set; }
  [Parameter] public RenderFragment? ToolbarRight { get; set; }
  
  [Parameter] public List<TItem> Items { get; set; } = [];
  [Parameter] public int TotalCount { get; set; }
  [Parameter] public List<int> PageSizes { get; set; } = [5, 10, 20, 50];
  [Parameter] public int StartPageSize { get; set; } = 10;
  [Parameter] public bool ShowPagination { get; set; } = true;
  [Parameter] public bool IsLoading { get; set; }
  [Parameter] public string EmptyText { get; set; } = "Немає даних";

  [Parameter] public string? InitialSortKey { get; set; }
  [Parameter] public bool InitialSortDescending { get; set; } = true;
  
  [Parameter] public EventCallback<ReloadEventArgs> OnReload { get; set; }
  [Parameter] public EventCallback<TItem> OnRowClick { get; set; }
  
  private readonly List<Column<TItem>> _columns = [];
  private int _pageSize;
  private int _pageNumber = 1;
  private string? _sortedColumnKey;
  private bool _isSortDescending;

  private int PageCount => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)_pageSize));

  protected override void OnInitialized()
  {
    _pageSize = StartPageSize;
    _sortedColumnKey = InitialSortKey;
    _isSortDescending = InitialSortDescending;
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
      await InvokeReload();
  }

  internal void AddColumn(Column<TItem> column)
  {
    _columns.Add(column);
    StateHasChanged();
  }

  internal void RemoveColumn(Column<TItem> column)
  {
    if (_columns.Remove(column))
      StateHasChanged();
  }

  /// <summary>
  /// Перезавантажити поточну сторінку (наприклад, після створення чи видалення запису).
  /// </summary>
  public Task Reload(bool toFirstPage = false)
  {
    if (toFirstPage)
      _pageNumber = 1;

    return InvokeReload();
  }

  private async Task OnSortClick(Column<TItem> column)
  {
    if (!column.CanSort)
      return;

    // Нова колонка - за зростанням, повторний клік - змінює напрямок.
    _isSortDescending = _sortedColumnKey == column.Key && !_isSortDescending;
    _sortedColumnKey = column.Key;
    _pageNumber = 1;
    
    await InvokeReload();
  }

  private async Task OnPageNumberClick(int pageNumber)
  {
    pageNumber = Math.Clamp(pageNumber, 1, PageCount);
    if (pageNumber == _pageNumber)
      return;

    _pageNumber = pageNumber;
    await InvokeReload();
  }
  
  private async Task OnPageSizeChange(ChangeEventArgs args)
  {
    if (!int.TryParse(args.Value?.ToString(), out var pageSize))
      return;

    _pageSize = pageSize;
    _pageNumber = 1;
    
    await InvokeReload();
  }

  private IEnumerable<int> VisiblePages()
  {
    const int window = 5;

    var start = Math.Max(1, _pageNumber - window / 2);
    var end = Math.Min(PageCount, start + window - 1);
    start = Math.Max(1, end - window + 1);

    return Enumerable.Range(start, end - start + 1);
  }
  
  private async Task InvokeReload()
  {
    var args = new ReloadEventArgs
    {
      PageSize = _pageSize,
      PageNumber = _pageNumber,
      SortColumnKey = _sortedColumnKey,
      SortIsDescending = _isSortDescending
    };
    
    await OnReload.InvokeAsync(args);

    // Якщо після видалення поточна сторінка стала порожньою - переходимо на останню доступну.
    if (Items.Count == 0 && _pageNumber > 1 && _pageNumber > PageCount)
    {
      _pageNumber = PageCount;
      await InvokeReload();
    }
  }
}
