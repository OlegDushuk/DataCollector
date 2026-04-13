namespace DataCollector.Domain.Entities;

public class DataProperty
{
  public Guid Id { get; set; }
  public Guid DataId { get; set; }
  public Guid DataPropertyConfigId { get; set; }
  public string Value { get; set; } = null!;
  
  public DateTime CreatedAt { get; set; }
}