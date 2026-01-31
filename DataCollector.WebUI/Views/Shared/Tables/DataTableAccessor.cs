namespace DataCollector.WebUI.Views.Shared.Tables;

public class DataTableAccessor(Action<ColumnConfig> initializer)
{
  public void InitializeColumn(ColumnConfig config)
  {
    initializer.Invoke(config);
  }
}