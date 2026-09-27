using DataCollector.Server.Application.ExternalContracts.Enums;

namespace DataCollector.Server.Application.ExternalContracts.Models;

public class EntityPropertyConfigDto
{
  public Guid Id { get; set; }
  public string Key { get; set; } = null!;
  public string Name { get; set; } = null!;
  public PropertyDataType DataType { get; set; }
  public DateTime CreatedAt { get; set; }
}
