namespace DataCollector.WebUI.Entities;

/// <summary>
/// Віджет дашборду в роботі: налаштування, розібрані відносно моделі (поля, умови, колонки, зведення, діаграма),
/// і лічильники змін, за якими компоненти даних розуміють, що треба перерахувати.
/// </summary>
/// <remarks>
/// Ті самі класи налаштувань, що й на сторінці записів (<see cref="SummarySettings"/>, <see cref="ChartSettings"/>,
/// <see cref="TableColumns"/>), тож і редактори в бічній панелі ті самі.
/// </remarks>
public sealed class WidgetState
{
  public Guid Id { get; } = Guid.NewGuid();
  public string Type { get; set; } = WidgetTypes.Table;
  public string? Title { get; set; }
  public int W { get; set; } = 6;
  public int H { get; set; } = 5;

  /// <summary>Модель, вибрана у віджеті (з API); null - не вибрана.</summary>
  public Guid? ConfigId { get; private set; }

  /// <summary>Завантажена модель; null - не вибрана, видалена або ще завантажується.</summary>
  public EntityConfig? Config { get; private set; }

  public List<FilterField> Fields { get; private set; } = [];
  public List<RecordFilterForm> Filters { get; set; } = [];
  public string? Search { get; set; }

  public TableColumns? Columns { get; private set; }
  public string SortKey { get; set; } = FilterField.CreatedAtKey;
  public bool SortDescending { get; set; } = true;
  public int PageSize { get; set; } = 10;

  public SummarySettings Summary { get; } = new();
  public ChartSettings Chart { get; } = new();
  public AggregateMetricForm Kpi { get; private set; } = new();

  /// <summary>Змінились умови, модель чи параметри даних - компоненти віджета роблять новий запит.</summary>
  public int DataVersion { get; private set; }

  /// <summary>Змінився лише вигляд діаграми - перемалювати без запиту.</summary>
  public int StyleVersion { get; private set; }

  /// <summary>Налаштування, з якими віджет прийшов з API: застосовуються, коли завантажиться модель.</summary>
  private WidgetSettings _saved = new();

  public static readonly IReadOnlyList<int> PageSizes = [5, 10, 20, 50];

  private WidgetState(Guid id) => Id = id;

  public static WidgetState From(DashboardWidget widget) => new(widget.Id == Guid.Empty ? Guid.NewGuid() : widget.Id)
  {
    Type = WidgetTypes.Find(widget.Type).Code,
    Title = widget.Title,
    W = DashboardGrid.NearestWidth(widget.W),
    H = DashboardGrid.NearestHeight(widget.H),
    ConfigId = widget.ConfigId,
    _saved = WidgetSettings.From(widget.Settings)
  };

  /// <summary>Новий віджет вибраного типу зі стандартним розміром.</summary>
  public static WidgetState Create(string type, Guid? configId)
  {
    var info = WidgetTypes.Find(type);
    return new WidgetState(Guid.NewGuid()) { Type = info.Code, W = info.DefaultWidth, H = info.DefaultHeight, ConfigId = configId };
  }

  /// <summary>Копія віджета (новий Id, ті самі налаштування).</summary>
  public WidgetState Clone()
  {
    var copy = new WidgetState(Guid.NewGuid()) { Type = Type, Title = Title, W = W, H = H, ConfigId = ConfigId, _saved = CurrentSettings() };
    copy.Bind(Config);
    return copy;
  }

  public string TypeLabel => WidgetTypes.Find(Type).Label;

  /// <summary>Заголовок картки: власна назва або назва моделі.</summary>
  public string DisplayTitle => !string.IsNullOrWhiteSpace(Title)
    ? Title!
    : Type == WidgetTypes.Monitoring
      ? Config is null ? TypeLabel : $"{TypeLabel}: {Config.Name}"
      : Config?.Name ?? TypeLabel;

  /// <summary>
  /// Застосувати модель: збережені налаштування звіряються з її полями (зайве відкидається).
  /// null - модель не вибрана або видалена: налаштування зберігаються як були.
  /// </summary>
  public void Bind(EntityConfig? config)
  {
    // Модель видалили, а віджет лишився - зберігаємо його налаштування незмінними.
    if (config is null)
    {
      Config = null;
      Fields = [];
      Columns = null;
      DataVersion++;
      return;
    }

    var saved = Config is null ? _saved : CurrentSettings();

    Config = config;
    ConfigId = config.Id;
    Fields = FilterField.ForConfig(config);

    Search = string.IsNullOrWhiteSpace(saved.Search) ? null : saved.Search;
    Filters = RecordsPageSettings.RestoreFilters(saved.Filters, Fields);

    Columns = TableColumns.For(config, saved.Columns);
    SortKey = Fields.Any(f => f.Key == saved.SortKey) ? saved.SortKey! : FilterField.CreatedAtKey;
    SortDescending = saved.SortKey is null || saved.SortDescending;
    PageSize = PageSizes.Contains(saved.PageSize) ? saved.PageSize : 10;

    Summary.ResetFor(config.Id);
    saved.Summary?.RestoreTo(Summary, Fields);

    Chart.ResetFor(config.Id);
    saved.Chart?.RestoreTo(Chart, Fields);

    Kpi = saved.Kpi is { } kpi && IsValidKpi(kpi) ? new AggregateMetricForm { Function = kpi.Function, Field = kpi.Field } : new();

    _apiFiltersSource = null;
    DataVersion++;
  }

  /// <summary>Інша модель у віджеті: умови й поля, яких у ній немає, відкидаються.</summary>
  public void ChangeModel(EntityConfig config)
  {
    if (Config?.Id == config.Id)
      return;

    // Налаштування з іншої моделі здебільшого не підходять - починаємо зі стандартних, але лишаємо пошук.
    _saved = new WidgetSettings { Search = Search };
    Config = null;
    Bind(config);
  }

  /// <summary>Без моделі (для віджетів, де модель - необов'язковий фільтр, як «Стан правил»).</summary>
  public void ClearModel()
  {
    if (ConfigId is null)
      return;

    _saved = new WidgetSettings();
    Config = null;
    ConfigId = null;
    Fields = [];
    Columns = null;
    DataVersion++;
  }

  public void ChangeType(string type)
  {
    if (Type == type)
      return;

    var info = WidgetTypes.Find(type);
    Type = info.Code;

    // Число в широкій високій картці чи таблиця в маленькій виглядають дивно - беремо розмір типу.
    W = info.DefaultWidth;
    H = info.DefaultHeight;
    DataVersion++;
  }

  /// <summary>Змінились дані (умови, пошук, групування, показник) - перерахувати.</summary>
  public void DataChanged()
  {
    _apiFiltersSource = null;
    DataVersion++;
  }

  public void StyleChanged() => StyleVersion++;

  private bool IsValidKpi(AggregateMetricForm kpi)
    => AggregateCatalog.Functions.Any(f => f.Code == kpi.Function) &&
       (kpi.Function == "count" && kpi.Field == "" || AggregateCatalog.FieldsFor(kpi.Function, Fields).Any(f => f.Key == kpi.Field));

  // Умови у форматі API будуються лише при зміні (як на сторінці записів).
  private List<RecordFilterForm>? _apiFiltersSource;
  private List<object> _apiFilters = [];

  public List<object> ApiFilters()
  {
    if (ReferenceEquals(_apiFiltersSource, Filters))
      return _apiFilters;

    _apiFiltersSource = Filters;
    return _apiFilters = Filters
      .Select(f => (Filter: f, Field: Fields.FirstOrDefault(x => x.Key == f.Field)))
      .Where(x => x.Field != null)
      .Select(x => x.Filter.ToApi(x.Field!))
      .ToList();
  }

  /// <summary>Поточні налаштування. Віджет без моделі зберігає те, з чим прийшов.</summary>
  /// <param name="onlyType">Лише частини, потрібні віджету його типу (для збереження в БД).</param>
  public WidgetSettings CurrentSettings(bool onlyType = false)
  {
    if (Config is null)
      return _saved;

    // Для БД - лише те, що потрібно віджету його типу: у JSON таблиці немає налаштувань діаграми тощо.
    var settings = new WidgetSettings
    {
      Search = string.IsNullOrWhiteSpace(Search) ? null : Search,
      Filters = Filters.Select(f => f.Clone()).ToList()
    };

    var type = onlyType ? Type : null;
    bool Needs(string part) => type is null || type == part;

    if (Needs(WidgetTypes.Table))
    {
      settings.Columns = Columns?.ToStates();
      settings.SortKey = SortKey;
      settings.SortDescending = SortDescending;
      settings.PageSize = PageSize;
    }

    if (Needs(WidgetTypes.Summary))
      settings.Summary = RecordsPageSettings.Save(Summary);

    if (Needs(WidgetTypes.Chart))
      settings.Chart = RecordsPageSettings.Save(Chart);

    if (Needs(WidgetTypes.Kpi))
      settings.Kpi = new AggregateMetricForm { Function = Kpi.Function, Field = Kpi.Field };

    return settings;
  }

  public DashboardWidget ToApi(int x, int y) => new()
  {
    Id = Id,
    ConfigId = ConfigId,
    Type = Type,
    Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim(),
    X = x,
    Y = y,
    W = W,
    H = H,
    Settings = CurrentSettings(onlyType: true).ToJson()
  };
}
