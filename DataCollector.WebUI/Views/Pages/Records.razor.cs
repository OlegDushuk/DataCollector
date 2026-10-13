using DataCollector.WebUI.Entities;
using DataCollector.WebUI.Helpers;
using DataCollector.WebUI.ServerApiServices;
using DataCollector.WebUI.Services;
using DataCollector.WebUI.Views.Base;
using DataCollector.WebUI.Views.Components.DataTable;
using DataCollector.WebUI.Views.Components.DataTable.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DataCollector.WebUI.Views.Pages;

/// <summary>
/// Сторінка записів моделі: таблиця, зведення й діаграма з фільтрами та бічною панеллю параметрів.
/// Код розкладено за обов'язками: тут - завантаження моделі, запити таблиці, фільтри й пошук, перемикання подань;
/// <c>Records.Settings.cs</c> - збереження вигляду в браузері, <c>Records.Export.cs</c> - експорт,
/// <c>Records.Editing.cs</c> - створення, редагування й видалення записів.
/// </summary>
public partial class Records : PageBase, IDisposable
{
  [Parameter] public string? ConfigKey { get; set; }

  [Inject] private EntityConfigApiService ConfigApi { get; set; } = null!;
  [Inject] private EntityRecordApiService RecordApi { get; set; } = null!;
  [Inject] private IJSRuntime JS { get; set; } = null!;
  [Inject] private BrowserStorage Storage { get; set; } = null!;

  private static readonly List<int> PageSizes = [5, 10, 20, 50];
  private int _pageSize = 10;

  private DataTable<EntityInstance>? _table;

  private List<EntityConfigListItem>? _configs;
  private EntityConfig? _config;

  private List<EntityInstance> _records = [];
  private int _totalCount;
  private bool _isLoading = true;

  // Запит поточної сторінки таблиці. Новий запит скасовує попередній, щоб застаріла відповідь
  // не перезаписала свіжу і сервер не рахував сторінку, яка вже не потрібна.
  private CancellationTokenSource? _reloadCts;

  // Фільтри: застосовані умови, текстовий пошук і стан панелі.
  private List<FilterField> _filterFields = [];
  private List<RecordFilterForm> _filters = [];
  private string? _search;
  // Бічна панель з фільтрами (і параметрами зведення) - не забирає висоту в таблиці.
  private bool _isPanelOpen;
  private string? _filterFocus;
  private CancellationTokenSource? _searchDebounce;

  private const int SearchDebounceMs = 400;

  private enum RecordsView
  {
    Table,
    Summary,
    Chart
  }

  /// <summary>Подання, що працюють від агрегації (зведення, діаграма): параметри - у бічній панелі.</summary>
  private bool IsAggregateView => _view is RecordsView.Summary or RecordsView.Chart;

  private RecordsView _view = RecordsView.Table;

  // Вкладки бічної панелі в режимі таблиці.
  private enum PanelTab
  {
    Filters,
    Columns
  }

  private PanelTab _panelTab = PanelTab.Filters;

  // Видимі колонки і їхній порядок; видимі поля моделі - це й проєкція запиту (fields).
  private TableColumns? _columns;

  // Поточне сортування таблиці (з останнього запиту) - експорт повторює його.
  private string? _sortKey = FilterField.CreatedAtKey;
  private bool _sortDescending = true;

  private const string ActionsColumnKey = "__actions";
  private readonly SummarySettings _summarySettings = new();
  private readonly ChartSettings _chartSettings = new();

  // Змінився лише вигляд діаграми (згладжування, заливка) - перемалювати без нового запиту.
  private int _chartStyleVersion;

  // Збільшується при кожній зміні фільтрів або пошуку - за ним зведення розуміє, що треба перерахувати.
  private int _filtersVersion;

  // Модель без полів показує окремий порожній стан (див. розмітку), тут - лише порожня вибірка.
  private string EmptyText => HasFilters ? "Немає записів, що відповідають умовам" : "Записів поки немає";

  private HashSet<string> ActiveFilterKeys => _filters.Select(f => f.Field).ToHashSet();
  private bool HasFilters => _filters.Count > 0 || !string.IsNullOrWhiteSpace(_search);

  protected override async Task OnParametersSetAsync()
  {
    Error = null;

    _configs ??= await LoadConfigs();
    if (_configs is null || _configs.Count == 0)
      return;

    // Без ключа в адресі відкриваємо модель, з якою працювали минулого разу (або першу).
    if (string.IsNullOrEmpty(ConfigKey))
    {
      var last = await Storage.GetString(RecordsPageSettings.LastModelKey);
      var target = _configs.FirstOrDefault(c => string.Equals(c.Key, last, StringComparison.OrdinalIgnoreCase)) ?? _configs[0];

      Navigation.NavigateTo($"/records/{target.Key}", replace: true);
      return;
    }

    if (_config != null && string.Equals(_config.Key, ConfigKey, StringComparison.OrdinalIgnoreCase))
      return;

    CancelReload();
    _config = null;
    _settingsRestored = false;
    _records = [];
    _totalCount = 0;
    ResetFilters();

    var requestedKey = ConfigKey;
    EntityConfig? loaded = null;
    await Try(async () => loaded = await ConfigApi.Get(requestedKey));

    // Поки модель завантажувалась, користувач міг вибрати іншу - застаріла відповідь не потрібна.
    if (!string.Equals(requestedKey, ConfigKey, StringComparison.OrdinalIgnoreCase))
      return;

    _config = loaded;
    if (_config != null)
    {
      _filterFields = FilterField.ForConfig(_config);
      _summarySettings.ResetFor(_config.Id);
      _chartSettings.ResetFor(_config.Id);
      _columns = TableColumns.For(_config, await Storage.GetString(TableColumns.StorageKey(_config.Id)));

      // Таблиця створюється вже з відновленими сортуванням, розміром сторінки й фільтрами,
      // тож перший запит одразу правильний (без зайвого запиту зі стандартними налаштуваннями).
      await RestoreSettings(_config);
      await Storage.SetString(RecordsPageSettings.LastModelKey, _config.Key);
    }
  }

  private async Task<List<EntityConfigListItem>?> LoadConfigs()
  {
    List<EntityConfigListItem>? result = null;
    await Try(async () => result = await ConfigApi.GetAll());
    return result;
  }

  private void OnConfigChange(ChangeEventArgs e)
  {
    Navigation.NavigateTo($"/records/{e.Value}");
  }

  private async Task OnReload(ReloadEventArgs args)
  {
    if (_config is null)
      return;

    CancelReload();
    var cts = _reloadCts = new CancellationTokenSource();
    var configId = _config.Id;

    _sortKey = args.SortColumnKey;
    _sortDescending = args.SortIsDescending;
    _pageSize = args.PageSize;

    _isLoading = true;
    try
    {
      await Try(async () =>
      {
        var page = await RecordApi.Query(
          configId, ApiFilters(), _search,
          args.PageNumber, args.PageSize, args.SortColumnKey, args.SortIsDescending, _columns?.ApiFields, cts.Token);

        _records = page.Items;
        _totalCount = page.TotalCount;
      });
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
      // Запит замінено новішим (інша сторінка, сортування, модель) - результат більше не потрібен,
      // а індикатор завантаження вимкне новий запит.
      return;
    }
    finally
    {
      if (_reloadCts == cts)
        _reloadCts = null;

      cts.Dispose();
    }

    _isLoading = false;
  }

  private FilterField? FindFilterField(string key) => _filterFields.FirstOrDefault(f => f.Key == key);

  // Умови у форматі API будуються лише при зміні умов: ApiFilters() викликається в розмітці на кожен рендер,
  // а список _filters при будь-якій зміні замінюється новим (тож досить порівняти посилання).
  private List<RecordFilterForm>? _apiFiltersSource;
  private List<object> _apiFilters = [];

  /// <summary>Застосовані умови у форматі API - однакові для таблиці, зведення, діаграми й експорту.</summary>
  private List<object> ApiFilters()
  {
    if (ReferenceEquals(_apiFiltersSource, _filters))
      return _apiFilters;

    _apiFiltersSource = _filters;
    return _apiFilters = _filters
      .Select(f => (Filter: f, Field: FindFilterField(f.Field)))
      .Where(x => x.Field != null)
      .Select(x => x.Filter.ToApi(x.Field!))
      .ToList();
  }

  private void ResetFilters()
  {
    _filters = [];
    _search = null;
    _filterFocus = null;
    CancelSearchDebounce();
  }

  private void TogglePanel()
  {
    _isPanelOpen = !_isPanelOpen;
    _filterFocus = null;
  }

  /// <summary>
  /// Перемикання "Таблиця / Зведення / Діаграма". Параметри зведення й діаграми живуть у бічній панелі,
  /// тож при переході до них панель відкривається.
  /// </summary>
  private void SwitchView(RecordsView view)
  {
    if (_view == view)
      return;

    _view = view;
    if (IsAggregateView)
      _isPanelOpen = true;
  }

  /// <param name="needsData">true - змінились вісь чи лінії (новий запит), false - лише вигляд.</param>
  private void OnChartSettingsChanged(bool needsData)
  {
    if (needsData)
      _filtersVersion++;
    else
      _chartStyleVersion++;
  }

  /// <summary>Значок фільтра в заголовку колонки: відкрити панель з умовою для цього поля.</summary>
  private void OnFilterColumnClick(string key)
  {
    _filterFocus = key;
    _panelTab = PanelTab.Filters;
    _isPanelOpen = true;
  }

  /// <summary>Панель лишається відкритою: зручно уточнювати умови, бачачи результат.</summary>
  private Task ApplyFilters(List<RecordFilterForm> filters)
  {
    _filters = filters;
    _filterFocus = null;
    return ReloadFromFirstPage();
  }

  private void OnSummarySettingsChanged()
  {
    // Той самий лічильник, що й для фільтрів: зведення перераховується при його зміні.
    _filtersVersion++;
  }

  private Task RemoveFilter(RecordFilterForm filter)
  {
    _filters = _filters.Where(f => f != filter).ToList();
    return ReloadFromFirstPage();
  }

  private Task ClearFilters()
  {
    ResetFilters();
    return ReloadFromFirstPage();
  }

  /// <summary>
  /// Пошук запускається не на кожну літеру, а через паузу після введення (debounce):
  /// менше запитів до сервера, а попередній незавершений запит скасовується в <see cref="OnReload"/>.
  /// </summary>
  private async Task OnSearchInput(ChangeEventArgs e)
  {
    _search = e.Value?.ToString();

    CancelSearchDebounce();
    var debounce = _searchDebounce = new CancellationTokenSource();

    try
    {
      await Task.Delay(SearchDebounceMs, debounce.Token);
    }
    catch (OperationCanceledException)
    {
      return;
    }

    await ReloadFromFirstPage();
  }

  private Task ReloadFromFirstPage()
  {
    _filtersVersion++;

    // У режимах зведення й діаграми таблиці немає (вони перераховуються самі за _filtersVersion).
    return _view == RecordsView.Table && _table != null ? _table.Reload(toFirstPage: true) : Task.CompletedTask;
  }

  private void CancelReload()
  {
    _reloadCts?.Cancel();
    _reloadCts = null;
  }

  private void CancelSearchDebounce()
  {
    _searchDebounce?.Cancel();
    _searchDebounce?.Dispose();
    _searchDebounce = null;
  }

  public void Dispose()
  {
    // Користувач пішов зі сторінки - незавершений запит більше нікому не потрібен.
    CancelReload();
    CancelSearchDebounce();
  }
}
