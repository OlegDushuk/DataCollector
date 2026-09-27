namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Часткове оновлення поля: null означає "не змінювати". Тип поля змінити не можна.
/// </summary>
public class EditPropertyCommand
{
  public string? Name { get; set; }
  public string? Key { get; set; }
}
