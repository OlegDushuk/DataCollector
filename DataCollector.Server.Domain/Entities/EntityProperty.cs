using System.Globalization;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Entities;

/// <summary>
/// Значення конкретного поля в конкретному записі.
/// Тип значення відповідає типу поля: Text -> string, Number -> decimal, Boolean -> bool; порожнє значення -> null.
/// </summary>
public class EntityProperty
{
  public const int MaxValueLength = 4000;

  /// <summary>Знаків після коми, які зберігає БД (DECIMAL(38, 10)); більше - округлюється.</summary>
  public const int NumberScale = 10;

  /// <summary>Межа модуля числа: 28 цифр цілої частини в DECIMAL(38, 10).</summary>
  private const decimal NumberLimit = 1e28m;

  public EntityPropertyConfig Config { get; }

  /// <summary>string, decimal, bool або null - залежно від типу поля.</summary>
  public object? Value { get; private set; }

  public EntityProperty(EntityPropertyConfig config)
  {
    Config = config;
  }

  /// <summary>
  /// Встановлює значення з текстового представлення (форма, JSON, імпорт), перевіряючи його відповідність типу поля.
  /// </summary>
  public void SetValue(string? value)
  {
    Value = Normalize(Config, value);
  }

  /// <summary>
  /// Відновлення значення з БД без повторної валідації.
  /// </summary>
  internal void Restore(object? value)
  {
    Value = value;
  }

  private static object? Normalize(EntityPropertyConfig config, string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    value = value.Trim();

    switch (config.Type)
    {
      case PropertyDataType.Number:
      {
        var normalized = value.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
          throw new DomainValidationException($"Поле '{config.Name}': '{value}' не є числом");

        if (Math.Abs(number) >= NumberLimit)
          throw new DomainValidationException($"Поле '{config.Name}': число '{value}' занадто велике");

        // Ділення на 1.000...0 прибирає незначущі нулі: 120.50 -> 120.5.
        return Math.Round(number, NumberScale) / 1.0000000000000000000000000000m;
      }
      case PropertyDataType.Boolean:
      {
        if (bool.TryParse(value, out var flag))
          return flag;

        return value switch
        {
          "1" => true,
          "0" => false,
          _ => throw new DomainValidationException($"Поле '{config.Name}': '{value}' не є логічним значенням (true/false)")
        };
      }
      default:
      {
        if (value.Length > MaxValueLength)
          throw new DomainValidationException($"Поле '{config.Name}': текст довший за {MaxValueLength} символів");

        return value;
      }
    }
  }
}
