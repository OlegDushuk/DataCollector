namespace DataCollector.WebUI.Entities;

public class EntityInstance
{
  public DateTime CreatedAt { get; set; }
  public EntityConfig Config { get; set; }
  public readonly List<EntityProperty> Properties = [];

  public void SetProperty(string key, string value)
  {
    Properties.Add(new EntityProperty()
    {
      Config = Config.Properties.FirstOrDefault(x => x.Key == key),
      Value = value
    });
  }
}