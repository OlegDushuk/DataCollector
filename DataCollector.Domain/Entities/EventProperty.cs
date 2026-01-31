namespace DataCollector.Domain.Entities;

public class EventProperty
{
  public Guid Id { get; set; }
  public Guid EventId { get; set; }
  public Guid EventPropertyConfigId { get; set; }
  public DateTime CreatedAt { get; set; }
}