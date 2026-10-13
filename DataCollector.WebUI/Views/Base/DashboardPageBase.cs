using DataCollector.WebUI.Entities;
using DataCollector.WebUI.ServerApiServices;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Base;

/// <summary>
/// Спільне для перегляду й редактора дашборду: завантаження дашборду і моделей його віджетів.
/// Кожна модель завантажується один раз, навіть якщо її показують кілька віджетів.
/// </summary>
public abstract class DashboardPageBase : PageBase
{
  [Parameter] public Guid Id { get; set; }

  [Inject] protected DashboardApiService DashboardApi { get; set; } = null!;
  [Inject] protected EntityConfigApiService ConfigApi { get; set; } = null!;

  protected Dashboard? Dashboard { get; set; }
  protected List<WidgetState> Widgets { get; set; } = [];

  /// <summary>Дашборд не знайдено (видалений або неправильна адреса).</summary>
  protected bool NotFound { get; private set; }

  /// <summary>Моделі віджетів ще завантажуються.</summary>
  protected bool LoadingModels { get; private set; }

  private readonly Dictionary<Guid, Task<EntityConfig?>> _configs = [];

  /// <summary>Дашборд і моделі віджетів. Віджети з'являються одразу, а дані - коли завантажиться їхня модель.</summary>
  protected async Task LoadDashboard()
  {
    NotFound = false;
    Dashboard? loaded;

    try
    {
      loaded = await DashboardApi.Get(Id);
    }
    catch (ApiException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      NotFound = true;
      return;
    }
    catch (ApiException e)
    {
      Error = e.Message;
      return;
    }

    Dashboard = loaded;
    Widgets = loaded.Widgets.Select(WidgetState.From).ToList();

    LoadingModels = true;
    StateHasChanged();

    await Task.WhenAll(Widgets.Select(BindWidget));
    LoadingModels = false;
  }

  private async Task BindWidget(WidgetState widget)
  {
    if (widget.ConfigId is { } configId)
      widget.Bind(await LoadConfig(configId));
  }

  /// <summary>Модель за Id (з кешу сторінки); null - модель видалено.</summary>
  protected Task<EntityConfig?> LoadConfig(Guid id)
  {
    if (!_configs.TryGetValue(id, out var task))
      _configs[id] = task = FetchConfig(id);

    return task;
  }

  private async Task<EntityConfig?> FetchConfig(Guid id)
  {
    try
    {
      return await ConfigApi.Get(id.ToString());
    }
    catch (ApiException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      return null;
    }
    catch (ApiException e)
    {
      Error = e.Message;
      return null;
    }
  }
}
