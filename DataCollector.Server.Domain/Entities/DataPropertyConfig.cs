using DataCollector.Domain.Enums;

namespace DataCollector.Domain.Entities;

public class DataPropertyConfig
{
  public Guid Id { get; set; }
  public Guid DataConfigId { get; set; }
  public string Key { get; set; } = null!;
  public DataType Type { get; set; }
  
  public DateTime CreatedAt { get; set; }
  public string Name { get; set; } = null!;
}