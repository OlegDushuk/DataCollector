using System.Globalization;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Entities;

/// <summary>
/// Значення конкретного поля в конкретному записі.
/// Значення зберігається рядком у нормалізованому вигляді:
/// Number - інваріантна культура ("12.5"), Boolean - "true"/"false", порожнє - null.
/// </summary>
public class EntityProperty
{
  public const int MaxValueLength = 4000;

  public Guid Id { get; private set; }
  public string? Value { get; private set; }
  public DateTime CreatedAt { get; private set; }
  
  public EntityPropertyConfig Config { get; private set; }
  
  public EntityProperty(
    EntityPropertyConfig config,
    string? value = null,
    Guid? id = null,
    DateTime? createdAt = null)
  {
    Id = id ?? Guid.NewGuid();
    CreatedAt = createdAt ?? DateTime.UtcNow;
    Config = config;

    SetValue(value);
  }

  public void SetValue(string? value)
  {
    Value = Normalize(Config, value);
  }

  internal void Restore(Guid id, string? value, DateTime createdAt)
  {
    Id = id;
    Value = value;
    CreatedAt = createdAt;
  }

  private static string? Normalize(EntityPropertyConfig config, string? value)
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

        return number.ToString(CultureInfo.InvariantCulture);
      }
      case PropertyDataType.Boolean:
      {
        if (bool.TryParse(value, out var flag))
          return flag ? "true" : "false";

        return value switch
        {
          "1" => "true",
          "0" => "false",
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
