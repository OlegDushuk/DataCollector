using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Querying;

/// <summary>Системні поля запису, за якими можна фільтрувати та сортувати.</summary>
public enum RecordSystemField
{
  CreatedAt,
  UpdatedAt
}

/// <summary>
/// Поле, до якого застосовується умова або сортування: поле моделі або системне поле запису.
/// </summary>
public sealed class RecordField
{
  public const string CreatedAtKey = "createdAt";
  public const string UpdatedAtKey = "updatedAt";

  /// <summary>Поле моделі; null - системне поле.</summary>
  public EntityPropertyConfig? Property { get; }

  public RecordSystemField? SystemField { get; }

  /// <summary>Тип значень поля (системні поля - DateTime).</summary>
  public PropertyDataType Type => Property?.Type ?? PropertyDataType.DateTime;

  public string Name => Property?.Name ?? (SystemField == RecordSystemField.CreatedAt ? "Створено" : "Змінено");

  private RecordField(EntityPropertyConfig? property, RecordSystemField? systemField)
  {
    Property = property;
    SystemField = systemField;
  }

  /// <summary>
  /// Знаходить поле за ключем: "createdAt" / "updatedAt" або ключ поля моделі (без урахування регістру).
  /// Ці ключі зарезервовані й не можуть бути ключами полів моделі.
  /// </summary>
  public static RecordField Resolve(EntityConfig config, string? key)
  {
    if (string.Equals(key, CreatedAtKey, StringComparison.OrdinalIgnoreCase))
      return new RecordField(null, RecordSystemField.CreatedAt);

    if (string.Equals(key, UpdatedAtKey, StringComparison.OrdinalIgnoreCase))
      return new RecordField(null, RecordSystemField.UpdatedAt);

    var property = string.IsNullOrWhiteSpace(key) ? null : config.FindProperty(key.Trim());

    return property is null
      ? throw new DomainValidationException($"Модель не має поля '{key}'")
      : new RecordField(property, null);
  }
}
