using DataCollector.Server.Domain.Enums;

namespace DataCollector.Server.Domain.Entities;

public class EntityPropertyConfig
{
  public Guid Id { get; private set; }
  public string Key { get; private set; }
  public PropertyDataType Type { get; private set; }
  
  public DateTime CreatedAt { get; private set; }
  public string Name { get; private set; }
  
  public EntityPropertyConfig(string key, string name, PropertyDataType type)
  {
    if (string.IsNullOrWhiteSpace(key))
      throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace", nameof(key));
    
    if (string.IsNullOrWhiteSpace(name))
      throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace", nameof(name));
    
    Id = Guid.NewGuid();
    CreatedAt = DateTime.UtcNow;
    Key = key;
    Name = name;
    Type = type;
  }
}