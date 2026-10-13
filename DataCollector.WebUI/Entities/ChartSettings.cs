using DataCollector.WebUI.Enums;

namespace DataCollector.WebUI.Entities;

/// <summary>Тип діаграми.</summary>
public static class ChartTypes
{
  public const string Line = "line";
  public const string Bar = "bar";
  public const string BarHorizontal = "barHorizontal";
  public const string Pie = "pie";
  public const string Heatmap = "heatmap";

  public static readonly (string Code, string Label, string Hint)[] All =
  [
    (Line, "Лінії", "Зміна в часі, тренд"),
    (Bar, "Стовпці", "Порівняння груп або періодів"),
    (BarHorizontal, "Смуги", "Рейтинг, довгі назви"),
    (Pie, "Кільце", "Частки цілого (до 6 частин)"),
    (Heatmap, "Теплова карта", "Два поля: вісь X × рядки")
  ];

  public static bool IsKnown(string? code) => All.Any(t => t.Code == code);
}

/// <summary>
/// Налаштування діаграми: тип, що по осі X (категорії), які показники, чи розбивати за значеннями поля
/// і як показувати. Дані - той самий records/aggregate, що й для зведення.
/// </summary>
public sealed class ChartSettings
{
  public string Type { get; set; } = ChartTypes.Line;

  /// <summary>Стовпці/лінії розбиття - одне над одним (частини цілого). Лише для кількості й суми.</summary>
  public bool Stack { get; set; }

  /// <summary>Категорії (не час і не діапазони) - за спаданням значення: рейтинг замість алфавіту.</summary>
  public bool SortByValue { get; set; }

  /// <summary>Найбільше частин кільця; решта складається в «Інші».</summary>
  public const int MaxPieSlices = 6;

  public const int MaxLines = 5;

  /// <summary>Найбільше ліній при розбитті за полем: далі кольори вже не розрізнити.</summary>
  public const int MaxSeries = 8;

  /// <summary>Найбільше точок на осі X (з урахуванням заповнених пропусків).</summary>
  public const int MaxPoints = 1000;

  public Guid ConfigId { get; set; }

  /// <summary>Поле осі X: дата (з інтервалом), число (точне або з кроком) чи перелік / так-ні / текст.</summary>
  public string XField { get; set; } = FilterField.CreatedAtKey;

  public string Interval { get; set; } = "day";
  public string? Step { get; set; }

  /// <summary>Лінії - показники (кількість, сума, середнє, мінімум, максимум).</summary>
  public List<AggregateMetricForm> Lines { get; set; } = [new()];

  /// <summary>Поле-перелік або так/ні: кожне його значення - окрема лінія першого показника.</summary>
  public string? SplitBy { get; set; }

  public bool Smooth { get; set; }
  public bool Area { get; set; }

  /// <summary>Періоди (і кроки) без записів показувати нулем для кількості й суми, а не пропускати.</summary>
  public bool FillGaps { get; set; } = true;

  /// <summary>Інша модель - стандартна діаграма: кількість записів по днях.</summary>
  public void ResetFor(Guid configId)
  {
    if (ConfigId == configId)
      return;

    ConfigId = configId;
    Type = ChartTypes.Line;
    Stack = false;
    SortByValue = false;
    XField = FilterField.CreatedAtKey;
    Interval = "day";
    Step = null;
    Lines = [new AggregateMetricForm()];
    SplitBy = null;
    Smooth = false;
    Area = false;
    FillGaps = true;
  }

  public AggregateDimensionForm XDimension => new() { Field = XField, Interval = Interval, Step = Step };

  /// <summary>Тип підтримує друге поле (розбиття на лінії/стовпці або рядки теплової карти).</summary>
  public bool UsesSplit => Type != ChartTypes.Pie && SplitBy != null;

  /// <summary>Тип показує лише один показник: кільце, теплова карта або розбиття за полем.</summary>
  public bool SingleMetric => Type is ChartTypes.Pie or ChartTypes.Heatmap || UsesSplit;

  /// <summary>Показники, що реально рахуються.</summary>
  public IReadOnlyList<AggregateMetricForm> ActiveLines => SingleMetric ? Lines.Take(1).ToList() : Lines;

  /// <summary>Накопичення має сенс лише для частин цілого: розбиття за полем і кількість / сума.</summary>
  public bool CanStack => Type is ChartTypes.Line or ChartTypes.Bar or ChartTypes.BarHorizontal &&
                          UsesSplit && IsAdditive(Lines.FirstOrDefault()?.Function);

  public bool IsStacked => Stack && CanStack;

  /// <summary>Кількість і суму можна складати (частини цілого); середнє, мінімум, максимум - ні.</summary>
  public static bool IsAdditive(string? function) => function is "count" or "sum";

  /// <summary>Сортування за значенням - для стовпців і смуг з категоріями (не час і не діапазони чисел).</summary>
  public bool CanSortByValue(FilterField? x)
    => Type is ChartTypes.Bar or ChartTypes.BarHorizontal && x != null && !AggregateDimensionForm.IsDate(x) &&
       !(x.DataType == PropertyDataType.Number && !string.IsNullOrWhiteSpace(Step));

  /// <summary>Групування для API: вісь X і (за потреби) поле розбиття.</summary>
  public List<object> ApiDimensions(IReadOnlyList<FilterField> fields)
  {
    var result = new List<object>();

    if (fields.FirstOrDefault(f => f.Key == XField) is { } x)
      result.Add(XDimension.ToApi(x));

    if (UsesSplit && fields.FirstOrDefault(f => f.Key == SplitBy) is { } split)
      result.Add(new { field = split.Key });

    return result;
  }

  /// <summary>Функції, доступні для типу: кільце показує частки, тож лише кількість і суму.</summary>
  public bool AllowsFunction(string function) => Type != ChartTypes.Pie || IsAdditive(function);

  public List<object> ApiMetrics() => ActiveLines.Select(m => m.ToApi()).ToList();

  /// <summary>Функції ліній: на діаграмі лише числові результати (мінімум/максимум дат тут не мають сенсу).</summary>
  public static IEnumerable<FilterField> FieldsForLine(string function, IEnumerable<FilterField> fields) => function == "count"
    ? fields
    : fields.Where(f => f is { IsSystem: false, DataType: PropertyDataType.Number });

  /// <summary>Розбивати на лінії можна за полем з небагатьма значеннями: перелік або так/ні.</summary>
  public static IEnumerable<FilterField> FieldsForSplit(IEnumerable<FilterField> fields, string xField)
    => fields.Where(f => f.Key != xField && !f.IsSystem && f.DataType is PropertyDataType.Enum or PropertyDataType.Boolean);
}
