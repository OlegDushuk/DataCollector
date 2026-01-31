using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Shared.Tables;

public class ColumnConfig(RenderFragment<dynamic> template)
{
  public RenderFragment<dynamic> Template => template;
}