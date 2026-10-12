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

public partial class Records : PageBase, IDisposable
{
  [Parameter] public string? ConfigKey { get; set; }

  [Inject] private EntityConfigApiService ConfigApi { get; set; } = null!;
  [Inject] private EntityRecordApiService RecordApi { get; set; } = null!;
  [Inject] private IJSRuntime JS { get; set; } = null!;
  [Inject] private BrowserStorage Storage { get; set; } = null!;

  // Налаштування сторінки для поточної моделі зберігаються після кожного рендеру, якщо змінились
  // (див. OnAfterRenderAsync): так не треба пам'ятати про збереження в кожному обробнику.
  private string? _savedSettingsJson;
  private bool _settingsRestored;

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
    Summary
  }

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

  private bool _isExporting;
  private string? _notice;
  private const string ActionsColumnKey = "__actions";
  private readonly SummarySettings _summarySettings = new();

  // Збільшується при кожній зміні фільтрів або пошуку - за ним зведення розуміє, що треба перерахувати.
  private int _filtersVersion;

  private string EmptyText => _config?.Properties.Count == 0
    ? "У моделі немає полів - додайте їх у налаштуваннях моделі"
    : HasFilters ? "Немає записів, що відповідають умовам" : "Записів поки немає";

  private HashSet<string> ActiveFilterKeys => _filters.Select(f => f.Field).ToHashSet();
  private bool HasFilters => _filters.Count > 0 || !string.IsNullOrWhiteSpace(_search);

  private bool _isFormOpen;
  private EntityInstance? _editing;
  private Dictionary<string, string?> _form = [];
  private string? _formError;

  private EntityInstance? _toDelete;
  private string? _deleteError;

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
    await Try(async () => _config = await ConfigApi.Get(ConfigKey));

    if (_config != null)
    {
      _filterFields = FilterField.ForConfig(_config);
      _summarySettings.ResetFor(_config.Id);
      _columns = TableColumns.For(_config, await Storage.GetString(TableColumns.StorageKey(_config.Id)));

      // Таблиця створюється вже з відновленими сортуванням, розміром сторінки й фільтрами,
      // тож перший запит одразу правильний (без зайвого запиту зі стандартними налаштуваннями).
      await RestoreSettings(_config);
      await Storage.SetString(RecordsPageSettings.LastModelKey, _config.Key);
    }
  }

  /// <summary>Відновлює налаштування моделі з браузера; без збережених - стандартні.</summary>
  private async Task RestoreSettings(EntityConfig config)
  {
    var saved = await Storage.Get<RecordsPageSettings>(RecordsPageSettings.StorageKey(config.Id));
    if (saved?.Version != RecordsPageSettings.CurrentVersion)
      saved = new RecordsPageSettings();

    _view = saved.View == "summary" ? RecordsView.Summary : RecordsView.Table;
    _isPanelOpen = saved.PanelOpen;
    _panelTab = saved.PanelTab == "columns" ? PanelTab.Columns : PanelTab.Filters;

    _search = string.IsNullOrWhiteSpace(saved.Search) ? null : saved.Search;
    _filters = saved.RestoreFilters(_filterFields);

    // Поле сортування могли видалити з моделі - тоді стандартне сортування.
    _sortKey = _filterFields.Any(f => f.Key == saved.SortKey) ? saved.SortKey : FilterField.CreatedAtKey;
    _sortDescending = saved.SortKey is null || saved.SortDescending;
    _pageSize = saved.PageSize is { } size && PageSizes.Contains(size) ? size : 10;

    saved.RestoreSummary(_summarySettings, _filterFields);

    _savedSettingsJson = BrowserStorage.Serialize(CurrentSettings());
    _settingsRestored = true;
  }

  private RecordsPageSettings CurrentSettings() => new()
  {
    View = _view == RecordsView.Summary ? "summary" : "table",
    PanelOpen = _isPanelOpen,
    PanelTab = _panelTab == PanelTab.Columns ? "columns" : "filters",
    Search = string.IsNullOrWhiteSpace(_search) ? null : _search,
    Filters = _filters.Select(f => f.Clone()).ToList(),
    SortKey = _sortKey,
    SortDescending = _sortDescending,
    PageSize = _pageSize,
    Summary = RecordsPageSettings.Save(_summarySettings)
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

  /// <summary>Застосовані умови у форматі API - однакові для таблиці і зведення.</summary>
  private List<object> ApiFilters() => _filters
    .Select(f => (Filter: f, Field: FindFilterField(f.Field)))
    .Where(x => x.Field != null)
    .Select(x => x.Filter.ToApi(x.Field!))
    .ToList();

  private void ResetFilters()
  {
    _filters = [];
    _search = null;
    _filterFocus = null;
    _searchDebounce?.Cancel();
  }

  private void TogglePanel()
  {
    _isPanelOpen = !_isPanelOpen;
    _filterFocus = null;
  }

  /// <summary>
  /// Перемикання "Таблиця / Зведення". Параметри зведення живуть у бічній панелі, тож при переході
  /// до зведення панель відкривається.
  /// </summary>
  private void SwitchView(RecordsView view)
  {
    if (_view == view)
      return;

    _view = view;
    if (view == RecordsView.Summary)
      _isPanelOpen = true;
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

    _searchDebounce?.Cancel();
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

    // У режимі зведення таблиці немає (воно перераховується саме за _filtersVersion).
    return _view == RecordsView.Table && _table != null ? _table.Reload(toFirstPage: true) : Task.CompletedTask;
  }

  private string ExportCaption => _view == RecordsView.Summary
    ? "Зведення з поточними групуваннями, показниками й фільтрами"
    : HasFilters
      ? $"Знайдені записи ({_totalCount:N0}), видимі колонки і сортування"
      : $"Усі записи ({_totalCount:N0}), видимі колонки і сортування";

  /// <summary>
  /// Експорт того, що відкрито: записи таблиці (умови, пошук, сортування, видимі колонки в їхньому порядку)
  /// або зведення (групування, показники, умови). Файл формує API, браузер отримує його через JS.
  /// </summary>
  private async Task Export(string format)
  {
    if (_config is null || _isExporting)
      return;

    _isExporting = true;
    _notice = null;
    Error = null;

    try
    {
      DownloadedFile? file = null;

      var ok = await Try(async () =>
      {
        file = _view == RecordsView.Summary
          ? await RecordApi.ExportAggregate(
            _config.Id, format, ApiFilters(), _search,
            _summarySettings.ApiDimensions(_filterFields), _summarySettings.ApiMetrics(), _summarySettings.Order)
          : await RecordApi.ExportRecords(
            _config.Id, format, ApiFilters(), _search, _sortKey, _sortDescending,
            _columns?.Visible.Select(c => c.Key).ToList());
      });

      if (!ok || file is null)
        return;

      using var stream = new MemoryStream(file.Content);
      using var reference = new DotNetStreamReference(stream);
      await JS.InvokeVoidAsync("dataCollector.downloadFile", file.FileName, file.ContentType, reference);

      if (file.Truncated)
        Error = $"У файл потрапили лише перші {file.RowCount:N0} записів - це ліміт одного експорту. Звузьте фільтри, щоб вивантажити решту.";
      else
        _notice = $"Файл {file.FileName} збережено" + (file.RowCount is { } rows ? $" ({rows:N0} рядків)" : "");
    }
    catch (JSException)
    {
      Error = "Браузер не зміг зберегти файл. Оновіть сторінку (Ctrl+F5) і спробуйте ще раз.";
    }
    finally
    {
      _isExporting = false;
    }
  }

  private void CancelReload()
  {
    _reloadCts?.Cancel();
    _reloadCts = null;
  }

  public void Dispose()
  {
    // Користувач пішов зі сторінки - незавершений запит більше нікому не потрібен.
    CancelReload();
    _searchDebounce?.Cancel();
  }

  private void OpenCreate()
  {
    _editing = null;

    // Новий запис одразу показує значення за замовчуванням - користувач бачить, що буде збережено.
    _form = _config!.Properties.ToDictionary(
      p => p.Key,
      p => FieldValue.ToForm(p.DataType, FieldValue.Raw(p.Rules.DefaultValue)));
    _formError = null;
    _isFormOpen = true;
  }

  /// <summary>
  /// Форма редагування завжди бере повний запис з сервера: у таблиці можуть бути лише вибрані поля,
  /// і збереження форми з неповними даними стерло б значення схованих полів.
  /// </summary>
  private async Task OpenEdit(EntityInstance row)
  {
    if (_config is null)
      return;

    var config = _config;
    EntityInstance? record = null;

    if (!await Try(async () => record = await RecordApi.Get(config.Id, row.Id)) || record is null || _config != config)
      return;

    _editing = record;
    _form = config.Properties.ToDictionary(p => p.Key, p => record.GetFormValue(p));
    _formError = null;
    _isFormOpen = true;
  }

  private async Task Save()
  {
    if (_config is null)
      return;

    // Значення надсилаємо рядками: сервер сам перевіряє їх за типом і правилами поля
    // (числа з комою або крапкою, межі, варіанти переліку), тож ця логіка не дублюється в інтерфейсі.
    // Інтерфейс лише переводить дату й час з місцевого часу в UTC.
    var values = _config.Properties.ToDictionary(
      p => p.Key,
      p => FieldValue.ToApi(p.DataType, _form[p.Key]));

    var ok = await Try(async () =>
    {
      if (_editing is null)
        await RecordApi.Create(_config.Id, values);
      else
        await RecordApi.Update(_config.Id, _editing.Id, values);
    }, e => _formError = e);

    if (!ok)
      return;

    var isNew = _editing is null;
    _isFormOpen = false;

    await RefreshData(toFirstPage: isNew);
  }

  private async Task Delete()
  {
    if (_config is null || _toDelete is null)
      return;

    var ok = await Try(() => RecordApi.Delete(_config.Id, _toDelete.Id), e => _deleteError = e);
    if (!ok)
      return;

    _toDelete = null;
    await RefreshData(toFirstPage: false);
  }

  /// <summary>Дані змінились: перечитати таблицю або перерахувати зведення - залежно від того, що відкрито.</summary>
  private Task RefreshData(bool toFirstPage)
  {
    if (_view == RecordsView.Summary || _table is null)
    {
      _filtersVersion++;
      return Task.CompletedTask;
    }

    return _table.Reload(toFirstPage);
  }
}
