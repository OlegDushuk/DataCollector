namespace DataCollector.Server.Domain.Monitoring;

/// <summary>Стан правила моніторингу (значення збігаються з колонкою State у БД).</summary>
public enum RuleState : byte
{
  /// <summary>Правило ще не перевірялось.</summary>
  Unknown = 0,

  /// <summary>Умова не виконується - усе гаразд.</summary>
  Ok = 1,

  /// <summary>Умова виконується - проблема.</summary>
  Alert = 2,

  /// <summary>Показник не вдалося порахувати: немає записів для середнього/мінімуму/максимуму або помилка (поле видалено).</summary>
  NoData = 3
}

/// <summary>Порівняння значення показника з порогом.</summary>
public enum RuleOperator
{
  GreaterThan,
  GreaterOrEqual,
  LessThan,
  LessOrEqual,
  Equal,
  NotEqual
}

/// <summary>Доставка сповіщення на зовнішній канал (значення збігаються з колонкою DeliveryStatus).</summary>
public enum DeliveryStatus : byte
{
  /// <summary>Зовнішній канал не налаштовано - сповіщення лише в інтерфейсі.</summary>
  NotRequired = 0,
  Pending = 1,
  Sent = 2,
  Failed = 3
}

public static class RuleOperators
{
  public static string Code(this RuleOperator op) => op switch
  {
    RuleOperator.GreaterThan => "gt",
    RuleOperator.GreaterOrEqual => "gte",
    RuleOperator.LessThan => "lt",
    RuleOperator.LessOrEqual => "lte",
    RuleOperator.Equal => "eq",
    _ => "neq"
  };

  public static string Symbol(this RuleOperator op) => op switch
  {
    RuleOperator.GreaterThan => ">",
    RuleOperator.GreaterOrEqual => "≥",
    RuleOperator.LessThan => "<",
    RuleOperator.LessOrEqual => "≤",
    RuleOperator.Equal => "=",
    _ => "≠"
  };

  public static RuleOperator? Parse(string? code) => code?.Trim().ToLowerInvariant() switch
  {
    "gt" or ">" => RuleOperator.GreaterThan,
    "gte" or ">=" => RuleOperator.GreaterOrEqual,
    "lt" or "<" => RuleOperator.LessThan,
    "lte" or "<=" => RuleOperator.LessOrEqual,
    "eq" or "=" => RuleOperator.Equal,
    "neq" or "!=" => RuleOperator.NotEqual,
    _ => null
  };

  public static bool Matches(this RuleOperator op, decimal value, decimal threshold) => op switch
  {
    RuleOperator.GreaterThan => value > threshold,
    RuleOperator.GreaterOrEqual => value >= threshold,
    RuleOperator.LessThan => value < threshold,
    RuleOperator.LessOrEqual => value <= threshold,
    RuleOperator.Equal => value == threshold,
    _ => value != threshold
  };
}
