using DataCollector.Server.Domain.Common;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Entities;

/// <summary>
/// Модель даних, яку налаштовує користувач: назва, ключ і набір полів.
/// </summary>
public class EntityConfig
{
  public Guid Id { get; private set; }
  public string Key { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public string Name { get; private set; }

  public IReadOnlyCollection<EntityPropertyConfig> Properties => _properties;
  private readonly List<EntityPropertyConfig> _properties = [];

  public EntityConfig(
    string key,
    string name,
    Guid? id = null,
    DateTime? createdAt = null,
    IEnumerable<EntityPropertyConfig>? properties = null)
  {
    Id = id ?? Guid.NewGuid();
    CreatedAt = createdAt ?? DateTime.UtcNow;
    Key = Guard.Key(key, "моделі");
    Name = Guard.Name(name, "моделі");

    if (properties != null)
      _properties.AddRange(properties);
  }

  public EntityPropertyConfig AddProperty(string key, string name, PropertyDataType dataType)
  {
    var prop = new EntityPropertyConfig(key, name, dataType);
    EnsureKeyIsFree(prop.Key, exceptPropertyId: null);

    _properties.Add(prop);
    return prop;
  }

  public EntityPropertyConfig? FindProperty(Guid propertyId)
    => _properties.FirstOrDefault(p => p.Id == propertyId);

  public EntityPropertyConfig? FindProperty(string key)
    => _properties.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

  public EntityPropertyConfig ChangeProperty(Guid propertyId, string? newKey, string? newName)
  {
    var prop = FindProperty(propertyId)
               ?? throw new DomainValidationException("Поле не належить цій моделі");

    if (newKey != null)
    {
      var key = Guard.Key(newKey, "поля");
      EnsureKeyIsFree(key, prop.Id);
      prop.ChangeKey(key);
    }

    if (newName != null)
      prop.ChangeName(newName);

    return prop;
  }

  public void RemoveProperty(Guid propertyId)
  {
    var prop = FindProperty(propertyId)
               ?? throw new DomainValidationException("Поле не належить цій моделі");

    _properties.Remove(prop);
  }

  public void ChangeName(string newName)
  {
    Name = Guard.Name(newName, "моделі");
  }

  public void ChangeKey(string newKey)
  {
    Key = Guard.Key(newKey, "моделі");
  }

  public EntityInstance CreateInstance() => new(this);

  // У БД колація CI, тому ключі порівнюємо без урахування регістру.
  private void EnsureKeyIsFree(string key, Guid? exceptPropertyId)
  {
    var taken = _properties.Any(p =>
      p.Id != exceptPropertyId && string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    if (taken)
      throw new DomainValidationException($"Поле з ключем '{key}' вже існує в цій моделі");
  }
}
