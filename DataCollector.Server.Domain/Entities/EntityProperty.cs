using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Entities;

/// <summary>
/// Значення конкретного поля в конкретному записі.
/// Тип значення відповідає типу поля (див. <see cref="Common.PropertyValueParser"/>); порожнє значення -> null.
/// </summary>
public class EntityProperty
{
  public EntityPropertyConfig Config { get; }

  /// <summary>string, decimal, bool, DateOnly, DateTime або null - залежно від типу поля.</summary>
  public object? Value { get; private set; }

  public EntityProperty(EntityPropertyConfig config)
  {
    Config = config;
  }

  /// <summary>
  /// Встановлює значення з текстового представлення (форма, JSON, імпорт), перевіряючи тип і правила поля.
  /// Очистити обов'язкове поле не можна.
  /// </summary>
  public void SetValue(string? value)
  {
    var parsed = Config.ParseValue(value);

    if (parsed is null && Config.IsRequired)
      throw RequiredError();

    Value = parsed;
  }

  internal void ApplyDefault()
  {
    Value = Config.DefaultValue;
  }

  internal DomainValidationException RequiredError()
    => new($"Поле '{Config.Name}' обов'язкове");

  /// <summary>
  /// Відновлення значення з БД без повторної валідації. Дата зберігається в одній колонці DATETIME2
  /// для обох типів дат, тому тут повертаємо їй правильний тип.
  /// </summary>
  internal void Restore(object? value)
  {
    Value = (Config.Type, value) switch
    {
      (PropertyDataType.Date, DateTime date) => DateOnly.FromDateTime(date),
      (PropertyDataType.DateTime, DateTime dateTime) => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
      _ => value
    };
  }
}
