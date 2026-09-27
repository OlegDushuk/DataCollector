using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DataCollector.WebUI.Views.Components.Button;

public partial class Button : ComponentBase
{
  [Parameter] public string Text { get; set; } = string.Empty;
  [Parameter] public EventCallback OnClick { get; set; }

  private async Task HandleClick(MouseEventArgs args)
  {
    if (OnClick.HasDelegate)
      await OnClick.InvokeAsync(args);
  }
}