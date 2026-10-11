namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Агрегація записів: групування і показники по групах. Умови фільтра й пошук - як у POST .../records/query.
/// </summary>
/// <example>
/// { "filters": [ { "field": "status", "operator": "neq", "value": "Скасоване" } ],
///   "groupBy": [ { "field": "createdAt", "interval": "month" } ],
///   "metrics": [ { "function": "count" }, { "function": "sum", "field": "price" } ],
///   "timeZone": "Europe/Kyiv" }
/// </example>
public class AggregateDataInstancesCommand
{
  public List<RecordFilterDto> Filters { get; set; } = [];
  public string? Search { get; set; }

  /// <summary>До двох групувань; порожньо - один рядок з підсумками по всіх записах.</summary>
  public List<AggregateDimensionDto> GroupBy { get; set; } = [];

  /// <summary>Від 1 до 10 показників; не передано - кількість записів.</summary>
  public List<AggregateMetricDto> Metrics { get; set; } = [];

  /// <summary>"keys" (за замовчуванням), "valueDesc" або "valueAsc" - за першим показником.</summary>
  public string? OrderBy { get; set; }

  /// <summary>Максимальна кількість груп (до 10 000).</summary>
  public int Limit { get; set; } = 1000;

  /// <summary>Часовий пояс (IANA або Windows) для меж інтервалів дат з часом; за замовчуванням UTC.</summary>
  public string? TimeZone { get; set; }
}

public class AggregateDimensionDto
{
  /// <summary>Ключ поля, "createdAt" або "updatedAt".</summary>
  public string Field { get; set; } = null!;

  /// <summary>Для дат: "hour", "day" (за замовчуванням), "week", "month", "quarter", "year".</summary>
  public string? Interval { get; set; }

  /// <summary>Для чисел: ширина діапазону (100 -> групи 0, 100, 200...); не задано - точні значення.</summary>
  public decimal? Step { get; set; }
}

public class AggregateMetricDto
{
  /// <summary>"count", "sum", "avg", "min", "max".</summary>
  public string Function { get; set; } = null!;

  /// <summary>Поле; для "count" необов'язкове (без поля - кількість записів, з полем - заповнених значень).</summary>
  public string? Field { get; set; }
}
