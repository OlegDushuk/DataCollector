namespace DataCollector.Domain.Entities;

public class DataConfig
{
  public Guid Id { get; set; }
  public string Key { get; set; } = null!;
  
  public DateTime CreatedAt { get; set; }
  public string Name { get; set; } = null!;
}