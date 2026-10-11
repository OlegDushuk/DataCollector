namespace DataCollector.Server.Application.ExternalContracts.Enums;

/// <summary>
/// Тип поля. У JSON передається назвою ("Number") або числом.
/// </summary>
public enum PropertyDataType
{
  Text = 0,
  Number = 1,
  Boolean = 2,

  /// <summary>Дата без часу: "2026-10-11".</summary>
  Date = 3,

  /// <summary>Дата й час в UTC: "2026-10-11T08:30:00Z".</summary>
  DateTime = 4,

  /// <summary>Значення з переліку варіантів (rules.options).</summary>
  Enum = 5
}
