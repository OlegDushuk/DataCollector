using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.Helpers;

/// <summary>
/// Опції Apache ECharts для лінійної діаграми з <see cref="ChartData"/>. Окремо від компонента,
/// щоб вигляд діаграми можна було перевірити без Blazor (тестовою сторінкою з тими ж опціями).
/// </summary>
/// <remarks>
/// У словниках лише присутні ключі: null у JSON ECharts сприйняв би як «вимкнути» або помилку.
/// Форматування чисел (uk-UA) додає JS-обгортка - функції в JSON не передаються.
/// </remarks>
public static class LineChartOptions
{
  /// <summary>
  /// Кольори ліній у фіксованому порядку (перший - акцент інтерфейсу). Палітра перевірена на розрізнення
  /// при порушеннях кольорового зору; три світлі кольори мають слабкий контраст з білим, тож лінії
  /// підписуються (легенда і підпис на кінці лінії), а не розрізняються лише кольором.
  /// </summary>
  public static readonly string[] Palette =
    ["#5248C8", "#EB6834", "#1BAF7A", "#EDA100", "#E87BA4", "#008300", "#2A78D6", "#E34948"];

  private const string GridColor = "#EDECF9";
  private const string AxisTextColor = "#A9A7C8";
  private const string LegendTextColor = "#6B6898";

  /// <summary>Точок на осі X більше - вмикається масштабування (колесо миші й повзунок).</summary>
  public const int ZoomFrom = 60;

  /// <summary>Точки-маркери видно лише на коротких рядах; на довгих вони зливаються в смугу.</summary>
  public const int SymbolsUpTo = 40;

  public static Dictionary<string, object> Build(ChartData data, ChartSettings settings)
  {
    var seriesCount = data.Series.Count;
    var showLegend = seriesCount > 1;

    // Підпис на кінці лінії - для 2-4 ліній: так їх видно без пошуку кольору в легенді.
    var endLabels = seriesCount is > 1 and <= 4;
    var zoom = data.Categories.Count > ZoomFrom;

    var option = new Dictionary<string, object>
    {
      ["color"] = Palette,
      ["animationDuration"] = 400,
      ["textStyle"] = new { fontFamily = "Inter, sans-serif" },
      ["grid"] = new
      {
        left = 8,
        right = endLabels ? 110 : 24,
        top = showLegend ? 40 : 16,
        bottom = zoom ? 48 : 8,
        containLabel = true
      },
      ["tooltip"] = new Dictionary<string, object>
      {
        ["trigger"] = "axis",
        ["axisPointer"] = new { type = "line", lineStyle = new { color = AxisTextColor } },
        ["order"] = "valueDesc"
      },
      ["xAxis"] = new
      {
        type = "category",
        data = data.Categories,
        boundaryGap = false,
        axisLine = new { lineStyle = new { color = GridColor } },
        axisTick = new { show = false },
        // Крайні підписи притиснуті до країв, щоб не обрізались (вісь без відступів - boundaryGap: false).
        axisLabel = new { color = AxisTextColor, hideOverlap = true, alignMinLabel = "left", alignMaxLabel = "right" }
      },
      ["yAxis"] = new Dictionary<string, object>
      {
        ["type"] = "value",
        ["splitLine"] = new { lineStyle = new { color = GridColor } },
        ["axisLabel"] = new { color = AxisTextColor }
      },
      ["series"] = data.Series.Select(s => Series(s, data.Categories.Count, endLabels, settings)).ToList()
    };

    if (showLegend)
    {
      option["legend"] = new
      {
        type = "scroll",
        top = 0,
        left = 0,
        icon = "roundRect",
        itemWidth = 14,
        itemHeight = 4,
        textStyle = new { color = LegendTextColor }
      };
    }

    if (zoom)
    {
      option["dataZoom"] = new object[]
      {
        new { type = "inside" },
        new { type = "slider", height = 18, bottom = 8, borderColor = GridColor, fillerColor = "rgba(82,72,200,0.12)" }
      };
    }

    return option;
  }

  private static Dictionary<string, object> Series(ChartSeries series, int points, bool endLabel, ChartSettings settings)
  {
    var result = new Dictionary<string, object>
    {
      ["name"] = series.Name,
      ["type"] = "line",
      // null у даних - розрив лінії (значення немає); ECharts позначає такі точки "-".
      ["data"] = series.Values.Select(v => v is { } value ? (object)value : "-").ToList(),
      ["smooth"] = settings.Smooth,
      ["showSymbol"] = points <= SymbolsUpTo,
      ["symbolSize"] = 8,
      ["lineStyle"] = new { width = 2 },
      ["emphasis"] = new { focus = "series" },
      ["connectNulls"] = false
    };

    if (settings.Area)
      result["areaStyle"] = new { opacity = 0.12 };

    if (endLabel)
      result["endLabel"] = new { show = true, formatter = "{a}", color = LegendTextColor, distance = 8 };

    return result;
  }
}
