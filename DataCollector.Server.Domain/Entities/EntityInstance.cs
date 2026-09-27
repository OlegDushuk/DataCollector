namespace DataCollector.Server.Domain.Entities;

public class EntityInstance
{
  public Guid Id { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public EntityConfig Config { get; private set; }
  
  public IReadOnlyCollection<EntityProperty> Properties => _properties;
  private readonly List<EntityProperty> _properties = [];

  public EntityInstance(
    EntityConfig config,
    Guid? id = null,
    DateTime? createdAt = null)
  {
    Config = config;
    Id = id ?? Guid.NewGuid();
    CreatedAt = createdAt ?? DateTime.UtcNow;

    foreach (var property in config.Properties)
      AddProperty(property);
  }
  
  public void SetPropertyValue(Guid propertyConfigId, string value)
  {
    var prop = _properties
      .FirstOrDefault(p => p.Config.Id == propertyConfigId);
    
    if (prop is null)
      throw new InvalidOperationException("Property not found");
    
    // prop.SetValue(value);
  }

  private void AddProperty(EntityPropertyConfig config)
  {
    var prop = new EntityProperty(config, this);
    _properties.Add(prop);
  }
}