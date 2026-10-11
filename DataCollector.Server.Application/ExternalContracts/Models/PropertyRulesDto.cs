namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Правила поля у відповіді API. Значення типізовані так само, як значення записів
/// (число, рядок, true/false, дата "2026-10-11", дата й час "2026-10-11T08:30:00Z").
/// </summary>
public class PropertyRulesDto
{
  public bool IsRequired { get; set; }
  public object? DefaultValue { get; set; }
  public object? Min { get; set; }
  public object? Max { get; set; }
  public int? MinLength { get; set; }
  public int? MaxLength { get; set; }
  public List<string>? Options { get; set; }
}
