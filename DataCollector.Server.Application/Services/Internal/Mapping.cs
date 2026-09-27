using System.Globalization;
using System.Text.Json;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Exceptions;
using ContractDataType = DataCollector.Server.Application.ExternalContracts.Enums.PropertyDataType;
using DomainDataType = DataCollector.Server.Domain.Enums.PropertyDataType;

namespace DataCollector.Server.Application.Services.Internal;

internal static class Mapping
{
  public static DomainDataType ToDomain(this ContractDataType type) => (DomainDataType)(int)type;
  public static ContractDataType ToContract(this DomainDataType type) => (ContractDataType)(int)type;

  public static EntityConfigDto ToDto(this EntityConfig config) => new()
  {
    Id = config.Id,
    Key = config.Key,
    Name = config.Name,
    CreatedAt = config.CreatedAt,
    Properties = config.Properties.Select(p => p.ToDto()).ToList()
  };

  public static EntityPropertyConfigDto ToDto(this EntityPropertyConfig property) => new()
  {
    Id = property.Id,
    Key = property.Key,
    Name = property.Name,
    DataType = property.Type.ToContract(),
    CreatedAt = property.CreatedAt
  };

  public static DataInstanceDto ToDto(this EntityInstance instance) => new()
  {
    Id = instance.Id,
    CreatedAt = instance.CreatedAt,
    UpdatedAt = instance.UpdatedAt,
    Values = instance.Properties.ToDictionary(p => p.Config.Key, p => ToTypedValue(p))
  };

  private static object? ToTypedValue(EntityProperty property)
  {
    if (property.Value is null)
      return null;

    return property.Config.Type switch
    {
      DomainDataType.Number when decimal.TryParse(
        property.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
      DomainDataType.Boolean when bool.TryParse(property.Value, out var flag) => flag,
      _ => property.Value
    };
  }

  /// <summary>
  /// Перетворює JSON-значення з запиту на рядок; доменна модель далі перевіряє відповідність типу поля.
  /// </summary>
  public static string? ToRawValue(this JsonElement element, string key)
  {
    return element.ValueKind switch
    {
      JsonValueKind.Null or JsonValueKind.Undefined => null,
      JsonValueKind.String => element.GetString(),
      JsonValueKind.Number => element.GetRawText(),
      JsonValueKind.True => "true",
      JsonValueKind.False => "false",
      _ => throw new DomainValidationException($"Поле '{key}': об'єкти та масиви не підтримуються")
    };
  }
}
