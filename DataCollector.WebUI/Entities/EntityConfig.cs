using DataCollector.WebUI.Enums;

namespace DataCollector.WebUI.Entities;

public class EntityConfig
{
  public Guid Id { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public string Key { get; private set; } = string.Empty;
  public string Name { get; private set; } = string.Empty;
  public List<EntityPropertyConfig> Properties = [];

  public void AddProperty(string name, string key, PropertyDataType dataType)
  {
    Properties.Add(new EntityPropertyConfig
      {
        Name = name,
        Key = key,
        Type = dataType
      }
    );
  }

  public EntityInstance CreateInstance()
  {
    var instance = new EntityInstance();
    instance.Config = this;
    
    return instance;
  }
}