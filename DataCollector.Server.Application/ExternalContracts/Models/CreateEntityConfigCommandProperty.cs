using DataCollector.Server.Application.ExternalContracts.Enums;

namespace DataCollector.Server.Application.ExternalContracts.Models;

public class CreateEntityConfigCommandProperty
{
  public string Name { get; set; } = null!;
  public string Key { get; set; } = null!;
  public PropertyDataType DataType { get; set; }
}