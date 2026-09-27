namespace DataCollector.Server.Application.ExternalContracts.Models;

public class EntityConfigDto
{
  public Guid Id { get; set; }
  public string Key { get; set; } = null!;
  public string Name { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public List<EntityPropertyConfigDto> Properties { get; set; } = [];
}
