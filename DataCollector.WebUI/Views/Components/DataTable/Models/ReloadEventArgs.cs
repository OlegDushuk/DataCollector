namespace DataCollector.WebUI.Views.Components.DataTable.Models;

public class ReloadEventArgs
{
  public int PageSize { get; set; }
  public int PageNumber { get; set; }
  
  public string? SortColumnKey { get; set; }
  public bool SortIsDescending { get; set; }
}
