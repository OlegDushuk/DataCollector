using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Entities;

/// <summary>
/// Запис моделі. Для кожного поля моделі має значення (може бути порожнім - null).
/// </summary>
public class EntityInstance
{
  public Guid Id { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public DateTime? UpdatedAt { get; private set; }

  /// <summary>
  /// Версія запису в сховищі (оптимістична конкурентність): змінюється при кожному збереженні.
  /// 0 - запис ще не збережено.
  /// </summary>
  public long Version { get; private set; }

  public EntityConfig Config { get; private set; }
  
  public IReadOnlyCollection<EntityProperty> Properties => _properties;
  private readonly List<EntityProperty> _properties = [];

  public EntityInstance(
    EntityConfig config,
    Guid? id = null,
    DateTime? createdAt = null,
    DateTime? updatedAt = null,
    long version = 0)
  {
    Config = config;
    Id = id ?? Guid.NewGuid();
    CreatedAt = createdAt ?? DateTime.UtcNow;
    UpdatedAt = updatedAt;
    Version = version;

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

  /// <summary>
  /// Новий запис: поля отримують значення за замовчуванням (передані значення потім їх перезапишуть).
  /// </summary>
  internal void ApplyDefaults()
  {
    foreach (var property in _properties)
      property.ApplyDefault();
  }

  /// <summary>
  /// Перевірка перед створенням запису: усі обов'язкові поля заповнені (передано значення або є значення за замовчуванням).
  /// При оновленні це не перевіряється - змінюються лише передані поля, а очистити обов'язкове поле не дає <see cref="EntityProperty.SetValue"/>.
  /// </summary>
  public void EnsureRequiredFilled()
  {
    var missing = _properties.FirstOrDefault(p => p.Config.IsRequired && p.Value is null);
    if (missing != null)
      throw missing.RequiredError();
  }

  public void MarkUpdated()
  {
    UpdatedAt = DateTime.UtcNow;
  }

  /// <summary>Нова версія після збереження (її видає сховище).</summary>
  public void SetVersion(long version)
  {
    Version = version;
  }

  /// <summary>
  /// Відновлення значення з БД без повторної валідації. Значення поля, якого вже немає в моделі, ігнорується.
  /// </summary>
  public void RestorePropertyValue(Guid propertyConfigId, object? value)
  {
    var prop = _properties.FirstOrDefault(p => p.Config.Id == propertyConfigId);
    prop?.Restore(value);
  }
}
