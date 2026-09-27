using DataCollector.WebUI.ServerApiServices;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Base;

public abstract class PageBase : ComponentBase
{
  [Inject] protected NavigationManager Navigation { get; set; } = null!;

  /// <summary>Помилка, яку показуємо вгорі сторінки.</summary>
  protected string? Error { get; set; }

  /// <summary>
  /// Виконує дію і перетворює помилку API на повідомлення. Повертає true, якщо все пройшло успішно.
  /// </summary>
  protected async Task<bool> Try(Func<Task> action, Action<string>? onError = null)
  {
    try
    {
      await action();
      return true;
    }
    catch (ApiException e)
    {
      if (onError != null)
        onError(e.Message);
      else
        Error = e.Message;

      return false;
    }
  }
}
