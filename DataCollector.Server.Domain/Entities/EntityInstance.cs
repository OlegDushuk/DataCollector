using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Entities;

/// <summary>
/// Запис моделі. Для кожного поля моделі має значення (може бути порожнім).
/// </summary>
public class EntityInstance
{
  public Guid Id { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public DateTime? UpdatedAt { get; private set; }
  public EntityConfig Config { get; private set; }
  
  public IReadOnlyCollection<EntityProperty> Properties => _properties;
  private readonly List<EntityProperty> _properties = [];

  public EntityInstance(
    EntityConfig config,
    Guid? id = null,
    DateTime? createdAt = null,
    DateTime? updatedAt = null)
  {
    Config = config;
    Id = id ?? Guid.NewGuid();
    CreatedAt = createdAt ?? DateTime.UtcNow;
    UpdatedAt = updatedAt;

    foreach (var property in config.Properties)
      _properties.Add(new EntityProperty(property));
  }

  public EntityProperty GetProperty(string key)
  {
    return _properties.FirstOrDefault(p => string.Equals(p.Config.Key, key, StringComparison.OrdinalIgnoreCase))
           ?? throw new DomainValidationException($"Модель '{Config.Key}' не має поля '{key}'");
  }

  public void SetPropertyValue(string key, string? value)
  {
    GetProperty(key).SetValue(value);
  }

  public void MarkUpdated()
  {
    UpdatedAt = DateTime.UtcNow;
  }

  /// <summary>
  /// Відновлення значення з БД без повторної валідації.
  /// </summary>
  public void RestorePropertyValue(Guid propertyConfigId, Guid valueId, string? value, DateTime createdAt)
  {
    var prop = _properties.FirstOrDefault(p => p.Config.Id == propertyConfigId);
    prop?.Restore(valueId, value, createdAt);
  }
}
