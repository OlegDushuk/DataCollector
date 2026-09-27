using DataCollector.Server.Domain.Enums;

namespace DataCollector.Server.Domain.Entities;

public class EntityConfig
{
  public Guid Id { get; private set; }
  public string Key { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public string Name { get; private set; }

  public IReadOnlyCollection<EntityPropertyConfig> Properties => _properties;
  private readonly List<EntityPropertyConfig> _properties = [];
  
  public EntityConfig(string key, string name, Guid? id = null, DateTime? createdAt = null)
  {
    if (string.IsNullOrWhiteSpace(key))
      throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace", nameof(key));
    
    if (string.IsNullOrWhiteSpace(name))
      throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace", nameof(name));

    Id = id ?? Guid.NewGuid();
    CreatedAt = createdAt ?? DateTime.UtcNow;
    Key = key;
    Name = name;
  }
  
  public void AddProperty(string key, string name, PropertyDataType dataType)
  {
    if (string.IsNullOrWhiteSpace(key))
      throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace", nameof(key));
    
    if (string.IsNullOrWhiteSpace(name))
      throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace", nameof(name));

    var prop = new EntityPropertyConfig(key, name, dataType);
    _properties.Add(prop);
  }
  
  public void ChangeName(string newName)
  {
    if (string.IsNullOrWhiteSpace(newName))
      throw new ArgumentException($"'{nameof(newName)}' cannot be null or whitespace", nameof(newName));
    
    Name = newName;
  }

  public void ChangeKey(string newKey)
  {
    if (string.IsNullOrWhiteSpace(newKey))
      throw new ArgumentException($"'{nameof(newKey)}' cannot be null or whitespace", nameof(newKey));
    
    Key = newKey;
  }
}