using System.Globalization;
using System.Text.Json;
using DataCollector.WebUI.Enums;
using DataCollector.WebUI.Helpers;

namespace DataCollector.WebUI.Entities;

/// <summary>Лінія діаграми: назва і значення для кожної категорії осі X (null - точки немає, лінія розривається).</summary>
public sealed record ChartSeries(string Name, double?[] Values);

/// <summary>
/// Дані для лінійної діаграми з результату агрегації: категорії осі X у правильному порядку
/// (з пропущеними періодами) і лінії.
/// </summary>
/// <remarks>
/// <para>
/// Агрегація повертає лише групи, де є записи. Для часу це означає «дірки»: дні без записів просто відсутні,
/// і лінія з'єднала б 1 і 5 жовтня, ніби між ними щось було. Тому послідовність періодів (і кроків числа)
/// від першого до останнього будується повністю, а відсутні точки стають 0 для кількості й суми
/// (записів справді не було) або порожніми для середнього, мінімуму й максимуму (значення немає).
/// </para>
/// <para>
/// З двома групуваннями (вісь X + поле розбиття) рядки «розгортаються»: кожне значення поля розбиття - окрема лінія.
/// </para>
/// </remarks>
public sealed class ChartData
{
  public List<string> Categories { get; } = [];
  public List<ChartSeries> Series { get; } = [];

  /// <summary>Пояснення під діаграмою: що обрізано або не показано.</summary>
  public List<string> Notes { get; } = [];

  private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

  /// <param name="xField">Поле осі X (для переліку - щоб показати всі варіанти).</param>
  /// <param name="splitField">Поле розбиття (для порядку ліній за варіантами переліку).</param>
  public static ChartData Build(AggregateResult result, ChartSettings settings, FilterField? xField, FilterField? splitField = null)
  {
    var data = new ChartData();
    if (result.Dimensions.Count == 0)
      return data;

    var x = result.Dimensions[0];
    var split = result.Dimensions.Count > 1 ? result.Dimensions[1] : null;
    var isTimeline = x.Interval != null;
    var isNumeric = x.DataType == PropertyDataType.Number && !isTimeline;

    // Записи без значення X на осі часу чи чисел не розмістити - показуємо їх кількість у примітці.
    var rows = result.Rows;
    if (isTimeline || isNumeric)
    {
      var withoutX = rows.Where(r => FieldValue.Raw(r.Keys[0]) is null).ToList();
      if (withoutX.Count > 0)
      {
        rows = rows.Except(withoutX).ToList();
        data.Notes.Add($"Записи без значення «{x.Name}» на діаграмі не показано.");
      }
    }

    var keys = CategoryKeys(rows, x, xField, settings.FillGaps, data.Notes);
    var index = keys.Select((k, i) => (k.Canonical, i)).ToDictionary(p => p.Canonical, p => p.i);

    foreach (var key in keys)
      data.Categories.Add(AggregateFormat.Key(x, key.Element));

    if (split is null)
    {
      for (var m = 0; m < result.Metrics.Count; m++)
      {
        var values = EmptyValues(keys.Count, result.Metrics[m], settings.FillGaps);
        foreach (var row in rows)
        {
          if (index.TryGetValue(Canonical(row.Keys[0], x), out var i))
            values[i] = Number(row.Values[m]);
        }

        data.Series.Add(new ChartSeries(result.Metrics[m].Name, values));
      }
    }
    else
    {
      var metric = result.Metrics[0];
      var groups = rows
        .GroupBy(r => Canonical(r.Keys[1], split))
        .Select(g => (Key: g.First().Keys[1], Rows: g.ToList(), Total: g.Sum(r => Number(r.Values[0]) ?? 0)))
        .ToList();

      // Найбільші лінії - у пріоритеті; решта не вміщається в палітру.
      var kept = groups.OrderByDescending(g => g.Total).Take(ChartSettings.MaxSeries).ToHashSet();
      if (kept.Count < groups.Count)
        data.Notes.Add($"Показано {kept.Count} найбільших ліній із {groups.Count}.");

      // Порядок ліній (і отже кольорів) - за порядком варіантів поля (так/ні - спершу «ні»), а не за величиною
      // чи появою в даних: так колір лишається за значенням, коли змінюються фільтри й період.
      var ordered = groups
        .Where(kept.Contains)
        .OrderBy(g => SplitOrder(g.Key, splitField))
        .ThenBy(g => FieldValue.Raw(g.Key), StringComparer.Ordinal);

      foreach (var group in ordered)
      {
        var values = EmptyValues(keys.Count, metric, settings.FillGaps);
        foreach (var row in group.Rows)
        {
          if (index.TryGetValue(Canonical(row.Keys[0], x), out var i))
            values[i] = Number(row.Values[0]);
        }

        data.Series.Add(new ChartSeries(AggregateFormat.Key(split, group.Key), values));
      }
    }

    if (result.Truncated)
      data.Notes.Add("Груп забагато - показано лише першу частину. Виберіть більший інтервал або звузьте фільтри.");

    return data;
  }

  /// <summary>
  /// Категорії за спаданням суми значень усіх ліній - рейтинг (найбільші групи першими).
  /// Порожні значення рахуються як 0.
  /// </summary>
  public void SortByTotal()
  {
    var order = Enumerable.Range(0, Categories.Count)
      .OrderByDescending(i => Series.Sum(s => s.Values[i] ?? 0))
      .ToList();

    var categories = order.Select(i => Categories[i]).ToList();
    Categories.Clear();
    Categories.AddRange(categories);

    for (var s = 0; s < Series.Count; s++)
      Series[s] = Series[s] with { Values = order.Select(i => Series[s].Values[i]).ToArray() };
  }

  private sealed record CategoryKey(string Canonical, JsonElement Element);

  /// <summary>Місце значення поля розбиття: індекс варіанта переліку; невідомі значення - після, порожнє - в кінці.</summary>
  private static int SplitOrder(JsonElement key, FilterField? field)
  {
    var raw = FieldValue.Raw(key);
    if (raw is null)
      return int.MaxValue;

    if (field?.DataType == PropertyDataType.Boolean)
      return raw == "true" ? 1 : 0;

    var index = field?.Options.ToList().IndexOf(raw) ?? -1;
    return index >= 0 ? index : int.MaxValue - 1;
  }

  /// <summary>Категорії осі X: повна послідовність періодів / кроків, варіанти переліку або значення в порядку сервера.</summary>
  private static List<CategoryKey> CategoryKeys(
    IReadOnlyList<AggregateRow> rows,
    AggregateDimensionInfo x,
    FilterField? field,
    bool fillGaps,
    List<string> notes)
  {
    var present = new List<CategoryKey>();
    var seen = new HashSet<string>();

    foreach (var row in rows)
    {
      var canonical = Canonical(row.Keys[0], x);
      if (seen.Add(canonical))
        present.Add(new CategoryKey(canonical, row.Keys[0]));
    }

    if (!fillGaps || present.Count == 0)
      return present;

    List<CategoryKey>? filled = null;

    if (x.Interval is { } interval)
      filled = Timeline(interval, present.Select(k => k.Canonical).ToList());
    else if (x.DataType == PropertyDataType.Number && x.Step is { } step)
      filled = Steps(step, present.Select(k => decimal.Parse(k.Canonical, CultureInfo.InvariantCulture)).ToList());
    else if (x.DataType == PropertyDataType.Enum && field?.Options is { Count: > 0 } options)
    {
      // Усі варіанти переліку в заданому порядку - і ті, де записів немає; потім невідомі значення й порожні.
      filled = options.Select(o => new CategoryKey(o, JsonSerializer.SerializeToElement(o))).ToList();
      filled.AddRange(present.Where(p => !options.Contains(p.Canonical)));
    }

    if (filled is null)
      return present;

    if (filled.Count > ChartSettings.MaxPoints)
    {
      notes.Add(string.Format(Ukrainian,
        "Для повної осі потрібно понад {0:N0} точок - пропуски не заповнено. Виберіть більший інтервал.", ChartSettings.MaxPoints));
      return present;
    }

    return filled;
  }

  /// <summary>Усі періоди від першого до останнього у форматі ключів API (2026-10-11, 2026-10, 2026-Q4...).</summary>
  private static List<CategoryKey>? Timeline(string interval, List<string> keys)
  {
    var dates = keys.Select(k => ParsePeriod(interval, k)).ToList();
    if (dates.Any(d => d is null))
      return null;

    var first = dates.Min()!.Value;
    var last = dates.Max()!.Value;
    var result = new List<CategoryKey>();

    for (var date = first; date <= last && result.Count <= ChartSettings.MaxPoints; date = NextPeriod(interval, date))
    {
      var key = FormatPeriod(interval, date);
      result.Add(new CategoryKey(key, JsonSerializer.SerializeToElement(key)));
    }

    return result;
  }

  private static List<CategoryKey> Steps(decimal step, List<decimal> values)
  {
    var result = new List<CategoryKey>();
    for (var value = values.Min(); value <= values.Max() && result.Count <= ChartSettings.MaxPoints; value += step)
    {
      var key = Trim(value);
      result.Add(new CategoryKey(key.ToString(CultureInfo.InvariantCulture), JsonSerializer.SerializeToElement(key)));
    }

    return result;
  }

  private static DateTime? ParsePeriod(string interval, string key)
  {
    var invariant = CultureInfo.InvariantCulture;

    switch (interval)
    {
      case "hour":
        return DateTime.TryParseExact(key, "yyyy-MM-dd'T'HH':00'", invariant, DateTimeStyles.None, out var hour) ? hour : null;
      case "month":
        return DateTime.TryParseExact(key, "yyyy-MM", invariant, DateTimeStyles.None, out var month) ? month : null;
      case "quarter" when key.Length == 7 && int.TryParse(key[..4], out var year) && int.TryParse(key[6..], out var quarter):
        return new DateTime(year, (quarter - 1) * 3 + 1, 1);
      case "year":
        return int.TryParse(key, out var y) ? new DateTime(y, 1, 1) : null;
      default:
        return DateTime.TryParseExact(key, "yyyy-MM-dd", invariant, DateTimeStyles.None, out var day) ? day : null;
    }
  }

  private static DateTime NextPeriod(string interval, DateTime date) => interval switch
  {
    "hour" => date.AddHours(1),
    "week" => date.AddDays(7),
    "month" => date.AddMonths(1),
    "quarter" => date.AddMonths(3),
    "year" => date.AddYears(1),
    _ => date.AddDays(1)
  };

  private static string FormatPeriod(string interval, DateTime date) => interval switch
  {
    "hour" => date.ToString("yyyy-MM-dd'T'HH':00'", CultureInfo.InvariantCulture),
    "month" => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
    "quarter" => $"{date.Year}-Q{(date.Month - 1) / 3 + 1}",
    "year" => date.Year.ToString(CultureInfo.InvariantCulture),
    _ => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
  };

  /// <summary>
  /// Значення ключа для порівняння: числа - без зайвих нулів (100 і 100.0 - одна група), порожнє - окремим маркером.
  /// </summary>
  private static string Canonical(JsonElement key, AggregateDimensionInfo dimension)
  {
    var raw = FieldValue.Raw(key);
    if (raw is null)
      return "\0null";

    if (dimension.DataType == PropertyDataType.Number && dimension.Interval is null &&
        decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
      return Trim(number).ToString(CultureInfo.InvariantCulture);

    return raw;
  }

  private static decimal Trim(decimal value) => value / 1.0000000000000000000000000000m;

  /// <summary>Порожня лінія: 0 там, де відсутність точки означає «нуль записів» (кількість, сума), інакше null.</summary>
  private static double?[] EmptyValues(int count, AggregateMetricInfo metric, bool fillGaps)
  {
    var values = new double?[count];
    if (fillGaps && metric.Function is "count" or "sum")
      Array.Fill(values, (double?)0);

    return values;
  }

  private static double? Number(JsonElement value)
    => AggregateFormat.Numeric(value) is { } number ? (double)number : null;
}
