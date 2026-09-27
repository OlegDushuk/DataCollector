using DataCollector.WebUI.Enums;

namespace DataCollector.WebUI.Entities;

public class EntityPropertyConfig
{
  public Guid Id { get; set; }
  public string Key { get; set; } = string.Empty;
  public string Name { get; set; } =  string.Empty;
  public PropertyDataType DataType { get; set; }
  public DateTime CreatedAt { get; set; }
}
