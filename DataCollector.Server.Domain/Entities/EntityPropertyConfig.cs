using DataCollector.Server.Domain.Common;
using DataCollector.Server.Domain.Enums;

namespace DataCollector.Server.Domain.Entities;

/// <summary>
/// Опис поля моделі: ключ, назва та тип даних.
/// </summary>
public class EntityPropertyConfig
{
  public Guid Id { get; private set; }
  public string Key { get; private set; }
  public PropertyDataType Type { get; private set; }
  
  public DateTime CreatedAt { get; private set; }
  public string Name { get; private set; }
  
  public EntityPropertyConfig(
    string key,
    string name,
    PropertyDataType type,
    Guid? id = null,
    DateTime? createdAt = null)
  {
    if (!Enum.IsDefined(type))
      throw new Exceptions.DomainValidationException($"Невідомий тип поля '{type}'");

    Id = id ?? Guid.NewGuid();
    CreatedAt = createdAt ?? DateTime.UtcNow;
    Key = Guard.Key(key, "поля");
    Name = Guard.Name(name, "поля");
    Type = type;
  }

  internal void ChangeKey(string newKey)
  {
    Key = Guard.Key(newKey, "поля");
  }

  internal void ChangeName(string newName)
  {
    Name = Guard.Name(newName, "поля");
  }
}
