using DataCollector.Server.Application.ExternalContracts.Enums;

namespace DataCollector.Server.Application.ExternalContracts.Models;

public class CreateEntityConfigCommandProperty
{
  public string Name { get; set; } = null!;
  public string Key { get; set; } = null!;
  public PropertyDataType DataType { get; set; }

  /// <summary>Правила поля; не передано - поле необов'язкове і без обмежень.</summary>
  public PropertyRulesCommand? Rules { get; set; }
}