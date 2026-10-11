using DataCollector.WebUI.Enums;
using DataCollector.WebUI.Helpers;

namespace DataCollector.WebUI.Entities;

/// <summary>
/// Стан редактора правил поля. Значення зберігаються у форматі полів форми;
/// при відправці в API лишаються тільки правила, доступні для вибраного типу.
/// </summary>
public class PropertyRulesForm
{
  public bool IsRequired { get; set; }
  public string? DefaultValue { get; set; }
  public string? Min { get; set; }
  public string? Max { get; set; }
  public string? MinLength { get; set; }
  public string? MaxLength { get; set; }

  /// <summary>Варіанти переліку, кожен з нового рядка.</summary>
  public string OptionsText { get; set; } = string.Empty;

  public List<string> Options => OptionsText
    .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
    .ToList();

  public static PropertyRulesForm From(PropertyDataType type, PropertyRules? rules)
  {
    if (rules is null)
      return new PropertyRulesForm();

    return new PropertyRulesForm
    {
      IsRequired = rules.IsRequired,
      DefaultValue = FieldValue.ToForm(type, FieldValue.Raw(rules.DefaultValue)),
      Min = FieldValue.ToForm(type, FieldValue.Raw(rules.Min)),
      Max = FieldValue.ToForm(type, FieldValue.Raw(rules.Max)),
      MinLength = rules.MinLength?.ToString(),
      MaxLength = rules.MaxLength?.ToString(),
      OptionsText = string.Join('\n', rules.Options ?? [])
    };
  }

  /// <summary>Тіло правил для API (PropertyRulesCommand).</summary>
  public object ToApi(PropertyDataType type)
  {
    var hasRange = type is PropertyDataType.Number or PropertyDataType.Date or PropertyDataType.DateTime;

    return new
    {
      isRequired = IsRequired,
      defaultValue = FieldValue.ToApi(type, DefaultValue),
      min = hasRange ? FieldValue.ToApi(type, Min) : null,
      max = hasRange ? FieldValue.ToApi(type, Max) : null,
      minLength = type == PropertyDataType.Text ? ParseInt(MinLength) : null,
      maxLength = type == PropertyDataType.Text ? ParseInt(MaxLength) : null,
      options = type == PropertyDataType.Enum ? Options : null
    };
  }

  private static int? ParseInt(string? value) => int.TryParse(value, out var number) ? number : null;
}
