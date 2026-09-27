using DataCollector.Server.Domain.Enums;

namespace DataCollector.Server.Domain.Entities;

public class EntityProperty
{
  public Guid Id { get; private set; }
  public string? Value { get; private set; }
  public DateTime CreatedAt { get; private set; }
  
  public EntityPropertyConfig Config { get; private set; }
  public EntityInstance Instance { get; private set; }
  
  public EntityProperty(
    EntityPropertyConfig config,
    EntityInstance instance,
    string? value = null,
    Guid? id = null,
    DateTime? createdAt = null)
  {
    Id = id ?? Guid.NewGuid();
    CreatedAt = createdAt ?? DateTime.UtcNow;
    Config = config;
    Instance = instance;

    switch (Config.Type)
    {
      case PropertyDataType.Boolean:
      {
        var res = bool.TryParse(value, out _);
      
        if (!res)
          throw new InvalidCastException($"Value '{value}' invalid to type '{Config.Type}'");
        break;
      }
      case PropertyDataType.Number:
      {
        var res = double.TryParse(value, out _);
        if (!res)
          throw new InvalidCastException($"Value '{value}' invalid to type '{Config.Type}'");
        break;
      }
    }
    
    Value = value;
  }
}