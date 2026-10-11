using System.Text.Json;

namespace DataCollector.WebUI.Entities;

/// <summary>
/// Правила поля з API. Значення (за замовчуванням, мінімум, максимум) приходять як JSON того ж типу, що й значення записів.
/// </summary>
public class PropertyRules
{
  public bool IsRequired { get; set; }
  public JsonElement? DefaultValue { get; set; }
  public JsonElement? Min { get; set; }
  public JsonElement? Max { get; set; }
  public int? MinLength { get; set; }
  public int? MaxLength { get; set; }
  public List<string>? Options { get; set; }
}
