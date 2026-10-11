using System.Text.Json;

namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Правила поля. Передаються повністю: при зміні поля новий набір правил замінює попередній.
/// </summary>
/// <remarks>
/// <c>defaultValue</c>, <c>min</c>, <c>max</c> приймаються в тому ж вигляді, що й значення записів:
/// число (<c>0</c>), рядок (<c>"2026-01-01"</c>) або <c>true</c>/<c>false</c>.
/// </remarks>
public class PropertyRulesCommand
{
  /// <summary>Усі типи: значення не може бути порожнім.</summary>
  public bool IsRequired { get; set; }

  /// <summary>Усі типи: значення для нових записів, якщо поле не передали.</summary>
  public JsonElement? DefaultValue { get; set; }

  /// <summary>Number, Date, DateTime: мінімум включно.</summary>
  public JsonElement? Min { get; set; }

  /// <summary>Number, Date, DateTime: максимум включно.</summary>
  public JsonElement? Max { get; set; }

  /// <summary>Text: мінімальна довжина.</summary>
  public int? MinLength { get; set; }

  /// <summary>Text: максимальна довжина (до 4000).</summary>
  public int? MaxLength { get; set; }

  /// <summary>Enum: допустимі варіанти (обов'язково для цього типу).</summary>
  public List<string>? Options { get; set; }
}
