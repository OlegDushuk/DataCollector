namespace DataCollector.Server.Application.ExternalContracts.Models;

public class AddPropertiesCommand
{
  public List<CreateEntityConfigCommandProperty> Properties { get; set; } = [];
}
