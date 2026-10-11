using DataCollector.WebUI.Entities;
using DataCollector.WebUI.Enums;

namespace DataCollector.WebUI.Helpers;

public static class Format
{
  public static string Date(DateTime value)
    => value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

  public static string Date(DateTime? value)
    => value.HasValue ? Date(value.Value) : "—";

  public static string DataType(PropertyDataType type) => type switch
  {
    PropertyDataType.Text => "Текст",
    PropertyDataType.Number => "Число",
    PropertyDataType.Boolean => "Так / Ні",
    PropertyDataType.Date => "Дата",
    PropertyDataType.DateTime => "Дата й час",
    PropertyDataType.Enum => "Перелік",
    _ => type.ToString()
  };

  /// <summary>
  /// Короткий опис правил поля для таблиці: "обов'язкове · 0 … 100 · за замовч.: 5".
  /// </summary>
  public static string Rules(EntityPropertyConfig property)
  {
    var rules = property.Rules;
    var type = property.DataType;
    var parts = new List<string>();

    if (rules.IsRequired)
      parts.Add("обов'язкове");

    var min = FieldValue.Raw(rules.Min);
    var max = FieldValue.Raw(rules.Max);
    if (min != null || max != null)
      parts.Add($"{(min is null ? "…" : FieldValue.Display(type, min))} – {(max is null ? "…" : FieldValue.Display(type, max))}");

    if (rules.MinLength != null || rules.MaxLength != null)
      parts.Add($"довжина {rules.MinLength ?? 0} – {rules.MaxLength?.ToString() ?? "…"}");

    if (rules.Options is { Count: > 0 } options)
      parts.Add(options.Count <= 4 ? string.Join(", ", options) : $"{options.Count} варіантів");

    var defaultValue = FieldValue.Raw(rules.DefaultValue);
    if (defaultValue != null)
      parts.Add($"за замовч.: {FieldValue.Display(type, defaultValue)}");

    return parts.Count == 0 ? "—" : string.Join(" · ", parts);
  }
}
