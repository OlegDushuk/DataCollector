using System.Text.Json;

namespace DataCollector.WebUI.Entities;

/// <summary>Рядок списку дашбордів.</summary>
public class DashboardListItem
{
  public Guid Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string? Description { get; set; }
  public int RefreshSeconds { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public int WidgetCount { get; set; }
}

/// <summary>Дашборд з віджетами - у тому вигляді, як його віддає й приймає API.</summary>
public class Dashboard
{
  public Guid Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string? Description { get; set; }
  public int RefreshSeconds { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public List<DashboardWidget> Widgets { get; set; } = [];
}

/// <summary>
/// Віджет у форматі API. Налаштування (<see cref="Settings"/>) - JSON, який сервер зберігає як є;
/// його структуру описує <see cref="WidgetSettings"/>.
/// </summary>
public class DashboardWidget
{
  public Guid Id { get; set; }
  public Guid? ConfigId { get; set; }
  public string Type { get; set; } = WidgetTypes.Table;
  public string? Title { get; set; }
  public int X { get; set; }
  public int Y { get; set; }
  public int W { get; set; }
  public int H { get; set; }
  public JsonElement Settings { get; set; }
}

/// <summary>
/// Налаштування віджета: те саме, що сторінка записів зберігає в браузері (фільтри, пошук, колонки, сортування,
/// зведення, діаграма), плюс показник для віджета-числа. Зберігається в БД разом з дашбордом.
/// </summary>
/// <remarks>
/// Поля моделі могли змінитись після збереження - при відкритті все звіряється з моделлю так само,
/// як налаштування сторінки записів (див. <see cref="RecordsPageSettings"/>): зайве відкидається.
/// </remarks>
public sealed class WidgetSettings
{
  public const int CurrentVersion = 1;

  public int Version { get; set; } = CurrentVersion;

  public string? Search { get; set; }
  public List<RecordFilterForm> Filters { get; set; } = [];

  /// <summary>Таблиця: видимість і порядок колонок (null - стандартні).</summary>
  public List<TableColumnState>? Columns { get; set; }

  public string? SortKey { get; set; }
  public bool SortDescending { get; set; } = true;
  public int PageSize { get; set; } = 10;

  public RecordsPageSettings.SavedSummary? Summary { get; set; }
  public RecordsPageSettings.SavedChart? Chart { get; set; }

  /// <summary>Віджет-число: функція і поле показника.</summary>
  public AggregateMetricForm? Kpi { get; set; }

  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
  {
    // Віджет зберігає лише частини свого типу (див. WidgetState.CurrentSettings) - решта не пишеться.
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
  };

  public JsonElement ToJson() => JsonSerializer.SerializeToElement(this, JsonOptions);

  /// <summary>Налаштування з JSON віджета; пошкоджені або іншої версії - стандартні.</summary>
  public static WidgetSettings From(JsonElement json)
  {
    if (json.ValueKind != JsonValueKind.Object)
      return new WidgetSettings();

    try
    {
      var settings = json.Deserialize<WidgetSettings>(JsonOptions);
      return settings?.Version == CurrentVersion ? settings : new WidgetSettings();
    }
    catch (JsonException)
    {
      return new WidgetSettings();
    }
  }
}

/// <summary>Типи віджетів, їхні назви і розміри за замовчуванням.</summary>
public static class WidgetTypes
{
  public const string Table = "table";
  public const string Summary = "summary";
  public const string Chart = "chart";
  public const string Kpi = "kpi";
  public const string Monitoring = "monitoring";

  public sealed record Info(string Code, string Label, string Hint, int DefaultWidth, int DefaultHeight);

  public static readonly IReadOnlyList<Info> All =
  [
    new(Chart, "Діаграма", "Лінії, стовпці, смуги, кільце або теплова карта", 6, 5),
    new(Kpi, "Показник", "Одне число: кількість, сума, середнє, мінімум або максимум", 3, 2),
    new(Summary, "Зведення", "Таблиця груп з показниками", 6, 5),
    new(Table, "Таблиця", "Записи з вибраними колонками і сортуванням", 12, 7),
    new(Monitoring, "Стан правил", "Правила моніторингу: що в нормі, а що спрацювало", 4, 5)
  ];

  /// <summary>Віджет бере дані не з моделі (модель - лише необов'язковий фільтр).</summary>
  public static bool IsModelOptional(string type) => type == Monitoring;

  public static Info Find(string? code) => All.FirstOrDefault(t => t.Code == code) ?? All.First(t => t.Code == Table);
}

/// <summary>
/// Сітка дашборду: 12 колонок, ряди фіксованої висоти. Ширина віджета - частка ширини, висота - кількість рядів.
/// </summary>
public static class DashboardGrid
{
  public const int Columns = 12;

  public static readonly IReadOnlyList<(int Value, string Label)> Widths =
  [
    (3, "1/4 ширини"),
    (4, "1/3 ширини"),
    (6, "1/2 ширини"),
    (8, "2/3 ширини"),
    (12, "Уся ширина")
  ];

  public static readonly IReadOnlyList<(int Value, string Label)> Heights =
  [
    (2, "Низька"),
    (3, "Невелика"),
    (5, "Середня"),
    (7, "Висока"),
    (10, "Дуже висока")
  ];

  /// <summary>Інтервали автооновлення перегляду, секунди (як дозволяє API).</summary>
  public static readonly IReadOnlyList<(int Value, string Label)> RefreshOptions =
  [
    (0, "Без автооновлення"),
    (30, "Кожні 30 с"),
    (60, "Щохвилини"),
    (300, "Кожні 5 хв"),
    (900, "Кожні 15 хв")
  ];

  public static int NearestWidth(int value) => Widths.MinBy(w => Math.Abs(w.Value - value)).Value;
  public static int NearestHeight(int value) => Heights.MinBy(h => Math.Abs(h.Value - value)).Value;

  /// <summary>
  /// Координати віджетів у порядку списку - так само, як CSS Grid розставляє елементи автоматично:
  /// курсор іде зліва направо і зверху вниз лише вперед, а кожен віджет стає на першу вільну ділянку
  /// від курсора (тож невисокий віджет займає місце під сусідом, а не лишає порожнечу).
  /// Позиції зберігаються в БД, щоб згодом перейти на вільне перетягування без зміни формату.
  /// </summary>
  public static List<(int X, int Y)> Arrange(IEnumerable<(int W, int H)> sizes)
  {
    var result = new List<(int X, int Y)>();
    var occupied = new List<bool[]>();
    int row = 0, column = 0;

    bool IsFree(int x, int y, int w, int h)
    {
      for (var r = y; r < y + h; r++)
      {
        if (r >= occupied.Count)
          return true;

        for (var c = x; c < x + w; c++)
        {
          if (occupied[r][c])
            return false;
        }
      }

      return true;
    }

    foreach (var (width, height) in sizes)
    {
      var w = Math.Clamp(width, 1, Columns);
      var h = Math.Max(1, height);

      // Перша вільна ділянка від курсора: у поточному ряду правіше, далі - з початку наступних рядів.
      while (column + w > Columns || !IsFree(column, row, w, h))
      {
        column++;
        if (column + w > Columns)
        {
          column = 0;
          row++;
        }
      }

      while (occupied.Count < row + h)
        occupied.Add(new bool[Columns]);

      for (var r = row; r < row + h; r++)
        for (var c = column; c < column + w; c++)
          occupied[r][c] = true;

      result.Add((column, row));
      column += w;
    }

    return result;
  }
}
