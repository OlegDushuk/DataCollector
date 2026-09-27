using DataCollector.WebUI.Enums;

namespace DataCollector.WebUI.Entities;

public class EntityPropertyConfig
{
  public string Key { get; set; } = string.Empty;
  public string Name { get; set; } =  string.Empty;
  public PropertyDataType Type { get; set; }
}