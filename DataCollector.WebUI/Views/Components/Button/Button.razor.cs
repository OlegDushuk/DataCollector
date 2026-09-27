using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DataCollector.WebUI.Views.Components;

public enum ButtonVariant
{
  Primary,
  Secondary,
  Danger,
  Ghost
}

public partial class Button : ComponentBase
{
  [Parameter] public string Text { get; set; } = string.Empty;
  [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
  [Parameter] public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;
  [Parameter] public bool Disabled { get; set; }
  [Parameter] public bool Small { get; set; }
  [Parameter] public string Type { get; set; } = "button";
  [Parameter] public string? Class { get; set; }
  [Parameter] public RenderFragment? Icon { get; set; }

  // Блокуємо кнопку, поки виконується асинхронний обробник, щоб уникнути подвійних кліків.
  private bool _isBusy;

  private async Task HandleClick(MouseEventArgs args)
  {
    if (!OnClick.HasDelegate || _isBusy)
      return;

    _isBusy = true;
    try
    {
      await OnClick.InvokeAsync(args);
    }
    finally
    {
      _isBusy = false;
    }
  }
}
