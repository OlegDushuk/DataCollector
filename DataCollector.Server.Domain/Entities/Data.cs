namespace DataCollector.Domain.Entities;

public class Data
{
  public Guid Id { get; set; }
  public Guid DataConfigId { get; set; }
  
  public DateTime CreatedAt { get; set; }
}