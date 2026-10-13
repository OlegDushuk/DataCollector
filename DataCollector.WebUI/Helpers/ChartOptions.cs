using DataCollector.WebUI.Entities;

namespace DataCollector.WebUI.Helpers;

/// <summary>
/// Опції Apache ECharts для всіх типів діаграм з <see cref="ChartData"/>: лінії, стовпці, смуги, кільце,
/// теплова карта. Окремо від компонента, щоб вигляд можна було перевірити без Blazor (тестовою сторінкою).
/// </summary>
/// <remarks>
/// <para>У словниках лише присутні ключі: null у JSON ECharts сприйняв би як «вимкнути» або помилку.</para>
/// <para>
/// Функції (форматування чисел uk-UA, підказки теплової карти) у JSON не передаються - їх додає JS-обгортка
/// (Assets/Js/charts.js). Правила вигляду: одна вісь Y; кольори категорій у фіксованому порядку і за значенням,
/// а не за величиною; 2px проміжок кольору тла між стовпцями й сегментами; заокруглений лише кінець стовпця;
/// кільце - не більше <see cref="ChartSettings.MaxPieSlices"/> частин; теплова карта - один відтінок від світлого до темного.
/// </para>
/// </remarks>
public static class ChartOptions
{
  /// <summary>
  /// Кольори категорій у фіксованому порядку (перший - акцент інтерфейсу). Палітра перевірена на розрізнення
  /// при порушеннях кольорового зору; три світлі кольори мають слабкий контраст з білим, тож серії
  /// підписуються (легенда, підписи), а не розрізняються лише кольором.
  /// </summary>
  public static readonly string[] Palette =
    ["#5248C8", "#EB6834", "#1BAF7A", "#EDA100", "#E87BA4", "#008300", "#2A78D6", "#E34948"];

  /// <summary>Послідовна шкала теплової карти: один відтінок (акцент), світліше - менше.</summary>
  public static readonly string[] Sequential = ["#F1F0FD", "#C9C5F1", "#9890E0", "#6A61D3", "#5248C8", "#2F2B63"];

  /// <summary>Колір «Інші» в кільці - нейтральний, щоб не сперечатися з категоріями.</summary>
  private const string OtherColor = "#C8C6DC";

  private const string Surface = "#FFFFFF";
  private const string GridColor = "#EDECF9";
  private const string AxisTextColor = "#A9A7C8";
  private const string LegendTextColor = "#6B6898";

  /// <summary>Точок на осі категорій більше - вмикається масштабування (колесо миші й повзунок).</summary>
  public const int ZoomFrom = 60;

  /// <summary>Точки-маркери ліній видно лише на коротких рядах; на довгих вони зливаються в смугу.</summary>
  public const int SymbolsUpTo = 40;

  public static Dictionary<string, object> Build(ChartData data, ChartSettings settings) => settings.Type switch
  {
    ChartTypes.Pie => Pie(data),
    ChartTypes.Heatmap => Heatmap(data),
    _ => Cartesian(data, settings)
  };

  /// <summary>Лінії, стовпці й смуги: вісь категорій + вісь значень.</summary>
  private static Dictionary<string, object> Cartesian(ChartData data, ChartSettings settings)
  {
    var isLine = settings.Type == ChartTypes.Line;
    var horizontal = settings.Type == ChartTypes.BarHorizontal;
    var stacked = settings.IsStacked;

    var seriesCount = data.Series.Count;
    var showLegend = seriesCount > 1;

    // Підпис на кінці лінії - для 2-4 ліній: так їх видно без пошуку кольору в легенді.
    var endLabels = isLine && !stacked && seriesCount is > 1 and <= 4;
    var zoom = data.Categories.Count > ZoomFrom;

    var categoryAxis = new Dictionary<string, object>
    {
      ["type"] = "category",
      ["data"] = data.Categories,
      // Лінія починається від краю; стовпці - посередині своїх клітинок.
      ["boundaryGap"] = !isLine,
      ["axisLine"] = new { lineStyle = new { color = GridColor } },
      ["axisTick"] = new { show = false },
      ["axisLabel"] = horizontal
        ? (object)new { color = AxisTextColor, hideOverlap = true, width = 150, overflow = "truncate" }
        : new { color = AxisTextColor, hideOverlap = true, alignMinLabel = isLine ? "left" : "center", alignMaxLabel = isLine ? "right" : "center" }
    };

    // Смуги: перша категорія вгорі (як у рейтингу), а не внизу.
    if (horizontal)
      categoryAxis["inverse"] = true;

    var valueAxis = new Dictionary<string, object>
    {
      ["type"] = "value",
      ["splitLine"] = new { lineStyle = new { color = GridColor } },
      ["axisLabel"] = new { color = AxisTextColor }
    };

    var option = Common(showLegend);
    option["grid"] = new
    {
      left = horizontal ? 16 : 8,
      right = endLabels ? 110 : 24,
      top = showLegend ? 40 : 16,
      bottom = zoom && !horizontal ? 48 : 8,
      containLabel = true
    };
    option["tooltip"] = new Dictionary<string, object>
    {
      ["trigger"] = "axis",
      ["axisPointer"] = isLine
        ? new { type = "line", lineStyle = new { color = AxisTextColor } }
        : new { type = "shadow", lineStyle = new { color = AxisTextColor } },
      ["order"] = stacked ? "seriesDesc" : "valueDesc"
    };
    option["xAxis"] = horizontal ? valueAxis : categoryAxis;
    option["yAxis"] = horizontal ? categoryAxis : valueAxis;
    // У стеку заокруглюється верхній ненульовий сегмент кожного стовпця (верхня серія буває порожньою).
    var topSeries = Enumerable.Range(0, data.Categories.Count)
      .Select(c => data.Series.FindLastIndex(s => s.Values[c] is > 0))
      .ToArray();

    option["series"] = data.Series
      .Select((s, i) => isLine
        ? LineSeries(s, data.Categories.Count, endLabels, stacked, settings)
        : BarSeries(s, i, topSeries, horizontal, stacked))
      .ToList();

    if (zoom)
    {
      option["dataZoom"] = horizontal
        ? new object[] { new { type = "inside", yAxisIndex = 0 } }
        : new object[]
        {
          new { type = "inside" },
          new { type = "slider", height = 18, bottom = 8, borderColor = GridColor, fillerColor = "rgba(82,72,200,0.12)" }
        };
    }

    return option;
  }

  private static Dictionary<string, object> LineSeries(ChartSeries series, int points, bool endLabel, bool stacked, ChartSettings settings)
  {
    var result = new Dictionary<string, object>
    {
      ["name"] = series.Name,
      ["type"] = "line",
      ["data"] = Values(series),
      ["smooth"] = settings.Smooth,
      ["showSymbol"] = points <= SymbolsUpTo,
      ["symbolSize"] = 8,
      ["lineStyle"] = new { width = 2 },
      ["emphasis"] = new { focus = "series" },
      ["connectNulls"] = false
    };

    if (stacked)
      result["stack"] = "total";

    // Накопичені лінії без заливки не читаються як частини цілого - заливка обов'язкова.
    if (settings.Area || stacked)
      result["areaStyle"] = new { opacity = stacked ? 0.35 : 0.12 };

    if (endLabel)
      result["endLabel"] = new { show = true, formatter = "{a}", color = LegendTextColor, distance = 8 };

    return result;
  }

  /// <summary>
  /// Стовпець: заокруглений лише кінець (4px), основа пряма; 2px проміжок кольору тла між сусідніми стовпцями
  /// і сегментами стеку. У стеку заокруглюється лише верхній ненульовий сегмент кожного стовпця.
  /// </summary>
  private static Dictionary<string, object> BarSeries(ChartSeries series, int index, int[] topSeries, bool horizontal, bool stacked)
  {
    int[] rounded = horizontal ? [0, 4, 4, 0] : [4, 4, 0, 0];

    // Без стеку - заокруглення для всіх стовпців серії; у стеку - окремо для кожної точки: верхній сегмент
    // заокруглений і без рамки (рамка кольору тла перекриває заокруглення), проміжок під ним дає рамка сегмента нижче.
    var data = stacked
      ? series.Values
        .Select((v, c) => (object)new
        {
          value = v is { } value ? (object)value : "-",
          itemStyle = topSeries[c] == index
            ? new { borderRadius = rounded, borderWidth = 0 }
            : new { borderRadius = new[] { 0, 0, 0, 0 }, borderWidth = 1 }
        })
        .ToList()
      : Values(series);

    var result = new Dictionary<string, object>
    {
      ["name"] = series.Name,
      ["type"] = "bar",
      ["data"] = data,
      ["barMaxWidth"] = 48,
      ["barGap"] = "8%",
      ["itemStyle"] = new { borderRadius = stacked ? [0, 0, 0, 0] : rounded, borderColor = Surface, borderWidth = 1 },
      ["emphasis"] = new { focus = "series" }
    };

    if (stacked)
      result["stack"] = "total";

    return result;
  }

  /// <summary>
  /// Кільце: частки першого показника за категоріями осі X. Найбільші частини - за спаданням,
  /// решта (понад <see cref="ChartSettings.MaxPieSlices"/>) - одна сіра частина «Інші». Порожні й нульові - не показуються.
  /// </summary>
  private static Dictionary<string, object> Pie(ChartData data)
  {
    var values = data.Series.FirstOrDefault()?.Values ?? [];
    var slices = data.Categories
      .Select((name, i) => (Name: name, Value: values.ElementAtOrDefault(i) ?? 0))
      .Where(s => s.Value > 0)
      .OrderByDescending(s => s.Value)
      .ToList();

    var items = new List<object>();
    var limit = slices.Count > ChartSettings.MaxPieSlices ? ChartSettings.MaxPieSlices - 1 : slices.Count;

    for (var i = 0; i < limit; i++)
      items.Add(new { name = slices[i].Name, value = slices[i].Value, itemStyle = new { color = Palette[i % Palette.Length] } });

    if (slices.Count > limit)
    {
      var rest = slices.Skip(limit).ToList();
      items.Add(new { name = $"Інші ({rest.Count})", value = rest.Sum(s => s.Value), itemStyle = new { color = OtherColor } });
    }

    var option = Common(showLegend: false);
    option["tooltip"] = new Dictionary<string, object> { ["trigger"] = "item" };
    option["series"] = new object[]
    {
      new Dictionary<string, object>
      {
        ["name"] = data.Series.FirstOrDefault()?.Name ?? string.Empty,
        ["type"] = "pie",
        ["radius"] = new[] { "45%", "70%" },
        ["percentPrecision"] = 1,
        ["center"] = new[] { "50%", "52%" },
        ["data"] = items,
        // Підпис зовні: назва і відсоток (значення - у підказці); 2px проміжок між частинами.
        ["label"] = new { color = LegendTextColor, formatter = "{b}\n{d}%", lineHeight = 16 },
        ["labelLine"] = new { lineStyle = new { color = AxisTextColor } },
        ["itemStyle"] = new { borderColor = Surface, borderWidth = 2 },
        ["emphasis"] = new { scale = true, scaleSize = 4 }
      }
    };

    return option;
  }

  /// <summary>
  /// Теплова карта: вісь X × значення поля розбиття (рядки), колір клітинки - величина першого показника
  /// за послідовною шкалою одного відтінку. Числа в клітинках - коли клітинок небагато.
  /// </summary>
  private static Dictionary<string, object> Heatmap(ChartData data)
  {
    var rows = data.Series.Select(s => s.Name).ToList();
    var cells = new List<object>();
    double? max = null, min = null;

    for (var y = 0; y < data.Series.Count; y++)
    {
      for (var x = 0; x < data.Categories.Count; x++)
      {
        var value = data.Series[y].Values[x];
        cells.Add(new object[] { x, y, value is { } v ? v : "-" });

        if (value is { } number)
        {
          max = max is null ? number : Math.Max(max.Value, number);
          min = min is null ? number : Math.Min(min.Value, number);
        }
      }
    }

    var showLabels = data.Categories.Count <= 24 && rows.Count <= 10;
    var zoom = data.Categories.Count > ZoomFrom;

    var option = Common(showLegend: false);
    option["tooltip"] = new Dictionary<string, object> { ["trigger"] = "item" };
    option["grid"] = new { left = 8, right = 24, top = 16, bottom = zoom ? 96 : 64, containLabel = true };
    option["xAxis"] = new
    {
      type = "category",
      data = data.Categories,
      splitArea = new { show = false },
      axisLine = new { lineStyle = new { color = GridColor } },
      axisTick = new { show = false },
      axisLabel = new { color = AxisTextColor, hideOverlap = true }
    };
    option["yAxis"] = new
    {
      type = "category",
      data = rows,
      inverse = true,
      axisLine = new { show = false },
      axisTick = new { show = false },
      axisLabel = new { color = LegendTextColor, width = 160, overflow = "truncate" }
    };
    option["visualMap"] = new
    {
      type = "continuous",
      min = Math.Min(0, min ?? 0),
      max = max is > 0 ? max.Value : 1,
      // Без повзунків: вони перекривають підписи шкали; фільтрувати клітинки за значенням тут не потрібно.
      calculable = false,
      text = new[] { "більше", "менше" },
      orient = "horizontal",
      left = "center",
      bottom = zoom ? 36 : 4,
      itemHeight = 200,
      itemWidth = 12,
      textStyle = new { color = AxisTextColor },
      inRange = new { color = Sequential }
    };
    option["series"] = new object[]
    {
      new Dictionary<string, object>
      {
        ["name"] = "heatmap",
        ["type"] = "heatmap",
        ["data"] = cells,
        ["label"] = new { show = showLabels, fontSize = 11 },
        // 2px проміжок кольору тла між клітинками.
        ["itemStyle"] = new { borderColor = Surface, borderWidth = 2, borderRadius = 2 },
        ["emphasis"] = new { itemStyle = new { borderColor = "#2F2B63", borderWidth = 1 } }
      }
    };

    if (zoom)
      option["dataZoom"] = new object[] { new { type = "slider", height = 18, bottom = 8, borderColor = GridColor } };

    return option;
  }

  private static Dictionary<string, object> Common(bool showLegend)
  {
    var option = new Dictionary<string, object>
    {
      ["color"] = Palette,
      ["animationDuration"] = 400,
      ["textStyle"] = new { fontFamily = "Inter, sans-serif" }
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

    return option;
  }

  /// <summary>null - точки немає (розрив лінії, порожній стовпець); ECharts позначає такі значення "-".</summary>
  private static List<object> Values(ChartSeries series)
    => series.Values.Select(v => v is { } value ? (object)value : "-").ToList();
}
