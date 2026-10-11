using System.Text.Json;
using DataCollector.WebUI.Helpers;

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
  /// Значення у вигляді рядка, як його повернуло API (число в інваріантній культурі, bool як "true"/"false",
  /// дата "2026-10-11", дата й час в UTC "2026-10-11T08:30:00Z").
  /// </summary>
  public string? GetRawValue(string key)
    => Values.TryGetValue(key, out var value) ? FieldValue.Raw(value) : null;

  /// <summary>Значення для поля форми редагування.</summary>
  public string? GetFormValue(EntityPropertyConfig property)
    => FieldValue.ToForm(property.DataType, GetRawValue(property.Key));

  public string GetDisplayValue(EntityPropertyConfig property)
    => FieldValue.Display(property.DataType, GetRawValue(property.Key));
}
