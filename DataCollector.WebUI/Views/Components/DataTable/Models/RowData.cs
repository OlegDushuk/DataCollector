namespace DataCollector.WebUI.Views.Components.DataTable.Models;

public class RowData
{
  public Dictionary<string, dynamic> Fields { get; set; } = new();
  
  public TField GetFieldByName<TField>(string fieldName)
  {
    return Fields[fieldName];
  }
}