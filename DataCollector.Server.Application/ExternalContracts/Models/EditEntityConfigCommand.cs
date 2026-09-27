namespace DataCollector.Server.Application.ExternalContracts.Models;

public class EditEntityConfigCommand
{
  public Guid EntityConfigId { get; set; }
  public string? Name { get; set; }
  public string? Key { get; set; }
}