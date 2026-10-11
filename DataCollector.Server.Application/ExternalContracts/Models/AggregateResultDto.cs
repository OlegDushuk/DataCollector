using DataCollector.Server.Application.ExternalContracts.Enums;

namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Результат агрегації: опис колонок і рядки (значення групувань і показників у тому ж порядку).
/// </summary>
public class AggregateResultDto
{
  public List<AggregateDimensionInfoDto> Dimensions { get; set; } = [];
  public List<AggregateMetricInfoDto> Metrics { get; set; } = [];
  public List<AggregateRowDto> Rows { get; set; } = [];

  /// <summary>Груп більше за ліміт - повернуто лише перші.</summary>
  public bool Truncated { get; set; }
}

public class AggregateDimensionInfoDto
{
  public string Field { get; set; } = null!;
  public string Name { get; set; } = null!;
  public PropertyDataType DataType { get; set; }
  public string? Interval { get; set; }
  public decimal? Step { get; set; }
}

public class AggregateMetricInfoDto
{
  public string Function { get; set; } = null!;
  public string? Field { get; set; }
  public string Name { get; set; } = null!;
}

public class AggregateRowDto
{
  /// <summary>
  /// Значення групувань: текст, число, true/false, null (порожнє значення) або підпис інтервалу дати
  /// ("2026-10-11", "2026-10-11T08:00", "2026-10", "2026-Q4", "2026"; тиждень - дата його понеділка).
  /// </summary>
  public List<object?> Keys { get; set; } = [];

  /// <summary>Значення показників: кількість - ціле число, сума/середнє - число, мінімум/максимум - число або дата.</summary>
  public List<object?> Values { get; set; } = [];
}
