using System.Text.Json;
using System.Text.Json.Serialization;
using DataCollector.Server.Domain.Entities;

namespace DataCollector.Server.DataAccess.Common;

/// <summary>
/// Правила поля, специфічні для його типу, зберігаються в колонці EntityPropertyConfigs.Settings як JSON:
/// <c>{"min":"0","max":"100"}</c>, <c>{"maxLength":200}</c>, <c>{"options":["Low","High"]}</c>.
/// Набір правил залежить від типу, тому окремі колонки для кожного були б здебільшого порожніми.
/// Загальні правила (обов'язковість, значення за замовчуванням) лежать в окремих колонках.
/// </summary>
internal static class PropertySettingsJson
{
  private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  /// <summary>JSON або null, якщо специфічних правил немає.</summary>
  public static string? Serialize(PropertyRules rules)
  {
    var settings = new Settings
    {
      Min = rules.Min,
      Max = rules.Max,
      MinLength = rules.MinLength,
      MaxLength = rules.MaxLength,
      Options = rules.Options is { Count: > 0 } ? rules.Options.ToList() : null
    };

    var isEmpty = settings is { Min: null, Max: null, MinLength: null, MaxLength: null, Options: null };
    return isEmpty ? null : JsonSerializer.Serialize(settings, Options);
  }

  public static PropertyRules ToRules(bool isRequired, string? defaultValue, string? json)
  {
    var settings = string.IsNullOrWhiteSpace(json)
      ? new Settings()
      : JsonSerializer.Deserialize<Settings>(json, Options) ?? new Settings();

    return new PropertyRules
    {
      IsRequired = isRequired,
      DefaultValue = defaultValue,
      Min = settings.Min,
      Max = settings.Max,
      MinLength = settings.MinLength,
      MaxLength = settings.MaxLength,
      Options = settings.Options
    };
  }

  private sealed class Settings
  {
    public string? Min { get; set; }
    public string? Max { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public List<string>? Options { get; set; }
  }
}
