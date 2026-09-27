namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Часткове оновлення моделі: null означає "не змінювати".
/// </summary>
public class EditEntityConfigCommand
{
  public string? Name { get; set; }
  public string? Key { get; set; }
}
