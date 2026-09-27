using System.Globalization;
using System.Text.Json;
using DataCollector.WebUI.Enums;

namespace DataCollector.WebUI.Entities;

/// <summary>
/// Запис моделі. Значення приходять з API як JSON (рядок / число / bool / null).
/// </summary>
public class EntityInstance
{
  public Guid Id { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public Dictionary<string, JsonElement> Values { get; set; } = [];

  /// <summary>
  /// Значення у вигляді рядка для полів форми (число в інваріантній культурі, bool як "true"/"false").
  /// </summary>
  public string? GetRawValue(string key)
  {
    if (!Values.TryGetValue(key, out var value))
      return null;

    return value.ValueKind switch
    {
      JsonValueKind.String => value.GetString(),
      JsonValueKind.Number => value.GetRawText(),
      JsonValueKind.True => "true",
      JsonValueKind.False => "false",
      _ => null
    };
  }

  public string GetDisplayValue(EntityPropertyConfig property)
  {
    var raw = GetRawValue(property.Key);
    if (raw is null)
      return "—";

    return property.DataType switch
    {
      PropertyDataType.Boolean => raw == "true" ? "Так" : "Ні",
      PropertyDataType.Number when decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
        => n.ToString("#,0.##########", CultureInfo.GetCultureInfo("uk-UA")),
      _ => raw
    };
  }
}
