namespace DataCollector.Server.Domain.Querying;

/// <summary>
/// Оператор умови фільтра. Набір допустимих операторів залежить від типу поля (див. <see cref="RecordFilter"/>).
/// </summary>
public enum FilterOperator
{
  Equals,
  NotEquals,
  GreaterThan,
  GreaterOrEqual,
  LessThan,
  LessOrEqual,

  /// <summary>Між двома значеннями включно.</summary>
  Between,

  Contains,
  NotContains,
  StartsWith,
  EndsWith,

  /// <summary>Дорівнює одному зі списку значень.</summary>
  In,
  NotIn,

  IsEmpty,
  IsNotEmpty
}
