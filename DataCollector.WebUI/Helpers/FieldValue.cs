using System.Globalization;
using System.Text.Json;
using DataCollector.WebUI.Enums;

namespace DataCollector.WebUI.Helpers;

/// <summary>
/// Перетворення значення поля між трьома представленнями:
/// API (JSON / текст в інваріантному форматі), поле форми (те, що приймає HTML input) і текст для показу.
/// </summary>
/// <remarks>
/// Дата й час в API - UTC, а у формі й таблиці - локальний час сервера інтерфейсу
/// (так само, як дати створення записів).
/// </remarks>
public static class FieldValue
{
  private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

  private const string FormDateTimeFormat = "yyyy-MM-ddTHH:mm";

  /// <summary>JSON-значення з API у вигляді рядка (число - в інваріантній культурі, bool - "true"/"false").</summary>
  public static string? Raw(JsonElement? value)
  {
    if (value is not { } element)
      return null;

    return element.ValueKind switch
    {
      JsonValueKind.String => element.GetString(),
      JsonValueKind.Number => element.GetRawText(),
      JsonValueKind.True => "true",
      JsonValueKind.False => "false",
      _ => null
    };
  }

  /// <summary>Значення з API -> значення поля форми.</summary>
  public static string? ToForm(PropertyDataType type, string? raw)
  {
    if (string.IsNullOrEmpty(raw))
      return null;

    return type switch
    {
      PropertyDataType.DateTime when TryParseUtc(raw, out var utc) => utc.ToLocalTime().ToString(FormDateTimeFormat, CultureInfo.InvariantCulture),
      PropertyDataType.Date when raw.Length > 10 => raw[..10],
      _ => raw
    };
  }

  /// <summary>Значення поля форми -> значення для API. Порожнє -> null.</summary>
  public static string? ToApi(PropertyDataType type, string? form)
  {
    if (string.IsNullOrWhiteSpace(form))
      return null;

    if (type == PropertyDataType.DateTime &&
        DateTime.TryParseExact(form, [FormDateTimeFormat, "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture,
          DateTimeStyles.AssumeLocal, out var local))
    {
      return local.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    // Число: кома або крапка, пробіли між розрядами (у т. ч. нерозривні - так його показує таблиця) прибираються.
    if (type == PropertyDataType.Number)
      return NormalizeNumber(form);

    return form.Trim();
  }

  /// <summary>Число з поля форми в інваріантному вигляді: "1 882,31" -> "1882.31".</summary>
  public static string NormalizeNumber(string value)
    => new string(value.Where(c => !char.IsWhiteSpace(c) && c != '\u202F').ToArray()).Replace(',', '.');

  /// <summary>Розбір числа з поля форми (кома або крапка); null - не число.</summary>
  public static decimal? ParseNumber(string? value)
    => !string.IsNullOrWhiteSpace(value) &&
       decimal.TryParse(NormalizeNumber(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
      ? number
      : null;

  /// <summary>Значення з API -> текст для таблиці.</summary>
  public static string Display(PropertyDataType type, string? raw)
  {
    if (string.IsNullOrEmpty(raw))
      return "—";

    return type switch
    {
      PropertyDataType.Boolean => raw == "true" ? "Так" : "Ні",
      PropertyDataType.Number when decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
        => n.ToString("#,0.##########", Ukrainian),
      PropertyDataType.Date when DateOnly.TryParse(raw[..Math.Min(10, raw.Length)], CultureInfo.InvariantCulture, out var date)
        => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
      PropertyDataType.DateTime when TryParseUtc(raw, out var utc)
        => utc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
      _ => raw
    };
  }

  private static bool TryParseUtc(string raw, out DateTime utc)
  {
    var ok = DateTime.TryParse(raw, CultureInfo.InvariantCulture,
      DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc);
    return ok;
  }
}
