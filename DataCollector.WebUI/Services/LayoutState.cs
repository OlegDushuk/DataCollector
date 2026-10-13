namespace DataCollector.WebUI.Services;

/// <summary>
/// Стан макета для поточного вікна (з'єднання Blazor): режим перегляду без меню й шапки.
/// Сторінка дашборду вмикає його, а <c>MainLayout</c> ховає бічне меню і шапку, лишаючи лише вміст.
/// </summary>
public sealed class LayoutState
{
  /// <summary>Меню й шапку сховано - на екрані лише вміст сторінки.</summary>
  public bool IsFocusMode { get; private set; }

  public event Action? Changed;

  public void SetFocusMode(bool value)
  {
    if (IsFocusMode == value)
      return;

    IsFocusMode = value;
    Changed?.Invoke();
  }
}
