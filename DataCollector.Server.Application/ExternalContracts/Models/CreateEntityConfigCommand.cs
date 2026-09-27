namespace DataCollector.Server.Application.ExternalContracts.Models;

public class CreateEntityConfigCommand
{
    public string Name { get; set; } = null!;
    public string Key { get; set; } = null!;
    public List<CreateEntityConfigCommandProperty> Properties { get; set; } = [];
}