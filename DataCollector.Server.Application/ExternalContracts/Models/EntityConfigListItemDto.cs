namespace DataCollector.Server.Application.ExternalContracts.Models;

public class EntityConfigListItemDto
{
  public Guid Id { get; set; }
  public string Key { get; set; } = null!;
  public string Name { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public int PropertyCount { get; set; }
  public int RecordCount { get; set; }
}
