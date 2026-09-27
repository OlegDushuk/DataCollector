namespace DataCollector.WebUI.Entities;

public class EntityConfigListItem
{
  public Guid Id { get; set; }
  public DateTime CreatedAt { get; set; }
  public string Key { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public int PropertyCount { get; set; }
  public int RecordCount { get; set; }
}
