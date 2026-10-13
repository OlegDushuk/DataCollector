namespace DataCollector.WebUI.Entities;

/// <summary>
/// Налаштування сторінки записів для однієї моделі, що зберігаються в браузері:
/// подання, фільтри й пошук, сортування і розмір сторінки, параметри зведення й діаграми, бічна панель.
/// Вибір колонок зберігається окремо (<see cref="TableColumns"/>).
/// </summary>
/// <remarks>
/// Поля, яких уже немає в моделі (видалили або змінили ключ), при відновленні відкидаються -
/// див. <see cref="RestoreFilters"/> і <see cref="RestoreSummary"/>.
/// </remarks>
public sealed class RecordsPageSettings
{
  /// <summary>Версія формату: якщо колись зміниться структура, старі налаштування просто ігноруються.</summary>
  public const int CurrentVersion = 1;

  public int Version { get; set; } = CurrentVersion;

  /// <summary>"table", "summary" або "chart".</summary>
  public string View { get; set; } = "table";

  public bool PanelOpen { get; set; }

  /// <summary>Вкладка панелі в режимі таблиці: "filters" або "columns".</summary>
  public string PanelTab { get; set; } = "filters";

  public string? Search { get; set; }
  public List<RecordFilterForm> Filters { get; set; } = [];

  public string? SortKey { get; set; }
  public bool SortDescending { get; set; } = true;
  public int? PageSize { get; set; }

  public SavedSummary? Summary { get; set; }
  public SavedChart? Chart { get; set; }

  public static string StorageKey(Guid configId) => $"records.{configId}";

  /// <summary>Ключ останньої відкритої моделі - сторінка «Записи» без моделі в адресі відкриває її.</summary>
  public const string LastModelKey = "records.lastModel";

  /// <summary>Умови для полів, що ще є в моделі, з оператором, допустимим для типу поля.</summary>
  public List<RecordFilterForm> RestoreFilters(IReadOnlyList<FilterField> fields)
  {
    return Filters
      .Where(f => fields.FirstOrDefault(x => x.Key == f.Field) is { } field && field.FindOperator(f.Operator) != null)
      .Select(f => f.Clone())
      .ToList();
  }

  /// <summary>
  /// Параметри зведення в <paramref name="target"/>: групування і показники лише за наявними полями.
  /// Якщо від показників нічого не лишилось - стандартна «кількість записів».
  /// </summary>
  public void RestoreSummary(SummarySettings target, IReadOnlyList<FilterField> fields)
  {
    if (Summary is null)
      return;

    bool Exists(string? key) => fields.Any(f => f.Key == key);

    target.Dimensions = Summary.Dimensions
      .Where(d => Exists(d.Field))
      .Take(2)
      .Select(d => new AggregateDimensionForm { Field = d.Field, Interval = d.Interval, Step = d.Step })
      .ToList();

    target.Metrics = Summary.Metrics
      .Where(m => AggregateCatalog.Functions.Any(f => f.Code == m.Function) && (m.Field == "" || Exists(m.Field)))
      .Take(5)
      .Select(m => new AggregateMetricForm { Function = m.Function, Field = m.Field })
      .ToList();

    if (target.Metrics.Count == 0)
      target.Metrics = [new AggregateMetricForm()];

    if (AggregateCatalog.Orders.Any(o => o.Code == Summary.Order))
      target.Order = Summary.Order;
  }

  public static SavedSummary Save(SummarySettings settings) => new()
  {
    Dimensions = settings.Dimensions.Select(d => new AggregateDimensionForm { Field = d.Field, Interval = d.Interval, Step = d.Step }).ToList(),
    Metrics = settings.Metrics.Select(m => new AggregateMetricForm { Function = m.Function, Field = m.Field }).ToList(),
    Order = settings.Order
  };

  /// <summary>Параметри діаграми в <paramref name="target"/>: лише за полями, що є в моделі.</summary>
  public void RestoreChart(ChartSettings target, IReadOnlyList<FilterField> fields)
  {
    if (Chart is null || fields.FirstOrDefault(f => f.Key == Chart.XField) is not { } x)
      return;

    target.Type = ChartTypes.IsKnown(Chart.Type) ? Chart.Type : ChartTypes.Line;
    target.Stack = Chart.Stack;
    target.SortByValue = Chart.SortByValue;
    target.XField = x.Key;
    target.Interval = AggregateDimensionForm.IsDate(x) && AggregateCatalog.IntervalsFor(x).Any(i => i.Code == Chart.Interval)
      ? Chart.Interval
      : "day";
    target.Step = Chart.Step;

    target.Lines = Chart.Lines
      .Where(m => AggregateCatalog.Functions.Any(f => f.Code == m.Function) &&
                  (m.Field == "" && m.Function == "count" || ChartSettings.FieldsForLine(m.Function, fields).Any(f => f.Key == m.Field)))
      .Take(ChartSettings.MaxLines)
      .Select(m => new AggregateMetricForm { Function = m.Function, Field = m.Field })
      .ToList();

    // Кільце показує частки - лише кількість і сума.
    if (target.Lines.Count == 0 || target.Type == ChartTypes.Pie && !ChartSettings.IsAdditive(target.Lines[0].Function))
      target.Lines = [new AggregateMetricForm()];

    target.SplitBy = ChartSettings.FieldsForSplit(fields, x.Key).Any(f => f.Key == Chart.SplitBy) ? Chart.SplitBy : null;
    target.Smooth = Chart.Smooth;
    target.Area = Chart.Area;
    target.FillGaps = Chart.FillGaps;
  }

  public static SavedChart Save(ChartSettings settings) => new()
  {
    Type = settings.Type,
    Stack = settings.Stack,
    SortByValue = settings.SortByValue,
    XField = settings.XField,
    Interval = settings.Interval,
    Step = settings.Step,
    Lines = settings.Lines.Select(m => new AggregateMetricForm { Function = m.Function, Field = m.Field }).ToList(),
    SplitBy = settings.SplitBy,
    Smooth = settings.Smooth,
    Area = settings.Area,
    FillGaps = settings.FillGaps
  };

  public sealed class SavedChart
  {
    public string Type { get; set; } = ChartTypes.Line;
    public bool Stack { get; set; }
    public bool SortByValue { get; set; }
    public string XField { get; set; } = FilterField.CreatedAtKey;
    public string Interval { get; set; } = "day";
    public string? Step { get; set; }
    public List<AggregateMetricForm> Lines { get; set; } = [];
    public string? SplitBy { get; set; }
    public bool Smooth { get; set; }
    public bool Area { get; set; }
    public bool FillGaps { get; set; } = true;
  }

  public sealed class SavedSummary
  {
    public List<AggregateDimensionForm> Dimensions { get; set; } = [];
    public List<AggregateMetricForm> Metrics { get; set; } = [];
    public string Order { get; set; } = "keys";
  }
}
