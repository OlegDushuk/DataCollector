namespace DataCollector.Domain.Entities;

public class Event
{
  public Guid Id { get; set; }
  public Guid EventConfigId { get; set; }
  public DateTime CreatedAt { get; set; }
}