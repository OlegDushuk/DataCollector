namespace DataCollector.Server.Domain.Enums;

/// <summary>
/// Тип поля. Числові значення зберігаються в БД (EntityPropertyConfigs.DataType), тому їх не можна змінювати.
/// </summary>
public enum PropertyDataType
{
  /// <summary>Рядок до 4000 символів.</summary>
  Text = 0,

  /// <summary>Десяткове число (DECIMAL(38, 10)).</summary>
  Number = 1,

  /// <summary>Так / ні.</summary>
  Boolean = 2,

  /// <summary>Календарна дата без часу.</summary>
  Date = 3,

  /// <summary>Дата й час, зберігаються в UTC.</summary>
  DateTime = 4,

  /// <summary>Одне значення з наперед заданого списку варіантів.</summary>
  Enum = 5
}
