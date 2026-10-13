using DataCollector.WebUI.ServerApiServices;

namespace DataCollector.WebUI.Services;

/// <summary>
/// Кількість непрочитаних сповіщень моніторингу для лічильника в меню (на вікно браузера).
/// Сторінка моніторингу оновлює її після «прочитано», лічильник у меню - підписаний на зміну.
/// </summary>
public sealed class MonitoringState(MonitoringApiService api)
{
  public int Unread { get; private set; }

  public event Action? Changed;

  /// <summary>Перечитати кількість з API (помилка зв'язку - лишається попереднє значення).</summary>
  public async Task Refresh(CancellationToken cancellationToken = default)
  {
    try
    {
      var count = await api.CountUnread(cancellationToken);
      if (count == Unread)
        return;

      Unread = count;
      Changed?.Invoke();
    }
    catch (ApiException)
    {
    }
  }
}
