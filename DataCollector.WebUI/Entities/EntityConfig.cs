namespace DataCollector.WebUI.Entities;

public class EntityConfig
{
  public Guid Id { get; set; }
  public DateTime CreatedAt { get; set; }
  public string Key { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public List<EntityPropertyConfig> Properties { get; set; } = [];
}
