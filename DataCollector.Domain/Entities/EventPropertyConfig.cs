namespace DataCollector.Domain.Entities;

public class EventPropertyConfig
{
  public Guid Id { get; set; }
  public Guid EventConfigId { get; set; }
  public DateTime CreatedAt { get; set; }
}