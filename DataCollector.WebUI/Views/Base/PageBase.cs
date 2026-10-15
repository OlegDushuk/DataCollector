using DataCollector.WebUI.ServerApiServices;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Base;

public abstract class PageBase : ComponentBase
{
  [Inject] protected NavigationManager Navigation { get; set; } = null!;

  /// <summary>Хто працює з інтерфейсом і що йому дозволено (кнопки змін показуються лише тим, кому API їх дозволить).</summary>
  [Inject] protected DataCollector.WebUI.Services.CurrentUser Me { get; set; } = null!;

  /// <summary>Помилка, яку показуємо вгорі сторінки.</summary>
  protected string? Error { get; set; }

  /// <summary>
  /// Остання дія через <see cref="Try"/> відхилена, бо об'єкт тим часом змінив хтось інший (409, оптимістична
  /// конкурентність) - сторінка може запропонувати перечитати актуальну версію.
  /// </summary>
  protected bool LastErrorIsConflict { get; private set; }

  /// <summary>
  /// Виконує дію і перетворює помилку API на повідомлення. Повертає true, якщо все пройшло успішно.
  /// </summary>
  protected async Task<bool> Try(Func<Task> action, Action<string>? onError = null)
  {
    LastErrorIsConflict = false;

    try
    {
      await action();
      return true;
    }
    catch (ApiException e)
    {
      LastErrorIsConflict = e.StatusCode == System.Net.HttpStatusCode.Conflict;

      if (onError != null)
        onError(e.Message);
      else
        Error = e.Message;

      return false;
    }
  }
}
