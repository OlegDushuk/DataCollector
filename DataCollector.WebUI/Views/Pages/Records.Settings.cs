using DataCollector.WebUI.Entities;
using DataCollector.WebUI.Services;

namespace DataCollector.WebUI.Views.Pages;

/// <summary>Сторінка записів: збереження й відновлення налаштувань вигляду в браузері (localStorage).</summary>
public partial class Records
{
  // Налаштування сторінки для поточної моделі зберігаються після кожного рендеру, якщо змінились
  // (див. OnAfterRenderAsync): так не треба пам'ятати про збереження в кожному обробнику.
  private string? _savedSettingsJson;
  private bool _settingsRestored;

  /// <summary>Відновлює налаштування моделі з браузера; без збережених - стандартні.</summary>
  private async Task RestoreSettings(EntityConfig config)
  {
    var saved = await Storage.Get<RecordsPageSettings>(RecordsPageSettings.StorageKey(config.Id));
    if (saved?.Version != RecordsPageSettings.CurrentVersion)
      saved = new RecordsPageSettings();

    _view = saved.View switch
    {
      "summary" => RecordsView.Summary,
      "chart" => RecordsView.Chart,
      _ => RecordsView.Table
    };
    _isPanelOpen = saved.PanelOpen;
    _panelTab = saved.PanelTab == "columns" ? PanelTab.Columns : PanelTab.Filters;

    _search = string.IsNullOrWhiteSpace(saved.Search) ? null : saved.Search;
    _filters = saved.RestoreFilters(_filterFields);

    // Поле сортування могли видалити з моделі - тоді стандартне сортування.
    _sortKey = _filterFields.Any(f => f.Key == saved.SortKey) ? saved.SortKey : FilterField.CreatedAtKey;
    _sortDescending = saved.SortKey is null || saved.SortDescending;
    _pageSize = saved.PageSize is { } size && PageSizes.Contains(size) ? size : 10;

    saved.RestoreSummary(_summarySettings, _filterFields);
    saved.RestoreChart(_chartSettings, _filterFields);

    _savedSettingsJson = BrowserStorage.Serialize(CurrentSettings());
    _settingsRestored = true;
  }

  private RecordsPageSettings CurrentSettings() => new()
  {
    View = _view switch
    {
      RecordsView.Summary => "summary",
      RecordsView.Chart => "chart",
      _ => "table"
    },
    PanelOpen = _isPanelOpen,
    PanelTab = _panelTab == PanelTab.Columns ? "columns" : "filters",
    Search = string.IsNullOrWhiteSpace(_search) ? null : _search,
    Filters = _filters.Select(f => f.Clone()).ToList(),
    SortKey = _sortKey,
    SortDescending = _sortDescending,
    PageSize = _pageSize,
    Summary = RecordsPageSettings.Save(_summarySettings),
    Chart = RecordsPageSettings.Save(_chartSettings)
  };

  /// <summary>
  /// Після кожного рендеру: якщо налаштування змінились (фільтр, сортування, подання, панель...) - зберегти.
  /// Порівняння за JSON дешеве, а запис у localStorage відбувається лише при реальній зміні.
  /// </summary>
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (_config is null || !_settingsRestored)
      return;

    var json = BrowserStorage.Serialize(CurrentSettings());
    if (json == _savedSettingsJson)
      return;

    _savedSettingsJson = json;
    await Storage.SetString(RecordsPageSettings.StorageKey(_config.Id), json);
  }

  /// <summary>Скинути вигляд моделі: подання, фільтри, сортування, колонки, зведення - як уперше.</summary>
  private async Task ResetView()
  {
    if (_config is null)
      return;

    await Storage.Remove(RecordsPageSettings.StorageKey(_config.Id));
    await Storage.Remove(TableColumns.StorageKey(_config.Id));

    // Найпростіше і найнадійніше - відкрити модель заново: таблиця створиться зі стандартними налаштуваннями.
    Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
  }

  /// <param name="needsData">Показано поле, значень якого ще немає в завантажених записах.</param>
  private async Task OnColumnsChanged(bool needsData)
  {
    if (_columns is null)
      return;

    await Storage.SetString(TableColumns.StorageKey(_columns.ConfigId), _columns.ToJson());

    if (needsData && _table != null)
      await _table.Reload();
  }
}
