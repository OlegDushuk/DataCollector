using System.Globalization;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Common;

/// <summary>
/// Перетворення значення поля між текстовим представленням (форма, JSON, імпорт, БД) і типізованим значенням.
/// </summary>
/// <remarks>
/// Типізовані значення за типом поля:
/// Text, Enum -> string; Number -> decimal; Boolean -> bool; Date -> DateOnly; DateTime -> DateTime (UTC).
/// </remarks>
public static class PropertyValueParser
{
  public const int MaxTextLength = 4000;

  /// <summary>Знаків після коми, які зберігає БД (DECIMAL(38, 10)); більше - округлюється.</summary>
  public const int NumberScale = 10;

  /// <summary>Межа модуля числа: 28 цифр цілої частини в DECIMAL(38, 10).</summary>
  private const decimal NumberLimit = 1e28m;

  private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy"];

  private static readonly string[] LocalDateTimeFormats =
  [
    "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss",
    "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss"
  ];

  /// <summary>
  /// Розбирає текст відповідно до типу поля. Порожній рядок -> null.
  /// </summary>
  /// <param name="field">Назва поля для повідомлення про помилку.</param>
  public static object? Parse(PropertyDataType type, string field, string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    value = value.Trim();

    return type switch
    {
      PropertyDataType.Number => ParseNumber(field, value),
      PropertyDataType.Boolean => ParseBool(field, value),
      PropertyDataType.Date => ParseDate(field, value),
      PropertyDataType.DateTime => ParseDateTime(field, value),
      _ => ParseText(field, value)
    };
  }

  /// <summary>
  /// Текстове представлення в інваріантному форматі: його ж приймає <see cref="Parse"/>.
  /// Використовується для збереження правил поля в БД.
  /// </summary>
  public static string? ToInvariantString(object? value) => value switch
  {
    null => null,
    string text => text,
    decimal number => number.ToString(CultureInfo.InvariantCulture),
    bool flag => flag ? "true" : "false",
    DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    DateTime dateTime => dateTime.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFZ", CultureInfo.InvariantCulture),
    _ => Convert.ToString(value, CultureInfo.InvariantCulture)
  };

  private static string ParseText(string field, string value)
  {
    if (value.Length > MaxTextLength)
      throw new DomainValidationException($"Поле '{field}': текст довший за {MaxTextLength} символів");

    return value;
  }

  private static decimal ParseNumber(string field, string value)
  {
    var normalized = value.Replace(',', '.');
    if (!decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
      throw new DomainValidationException($"Поле '{field}': '{value}' не є числом");

    if (Math.Abs(number) >= NumberLimit)
      throw new DomainValidationException($"Поле '{field}': число '{value}' занадто велике");

    // Ділення на 1.000...0 прибирає незначущі нулі: 120.50 -> 120.5.
    return Math.Round(number, NumberScale) / 1.0000000000000000000000000000m;
  }

  private static bool ParseBool(string field, string value)
  {
    if (bool.TryParse(value, out var flag))
      return flag;

    return value switch
    {
      "1" => true,
      "0" => false,
      _ => throw new DomainValidationException($"Поле '{field}': '{value}' не є логічним значенням (true/false)")
    };
  }

  /// <summary>
  /// Дата: 2026-10-11 або 11.10.2026. Якщо передано дату з часом (2026-10-11T08:00:00), час відкидається.
  /// </summary>
  private static DateOnly ParseDate(string field, string value)
  {
    if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
      return date;

    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
      return DateOnly.FromDateTime(dateTime);

    throw new DomainValidationException($"Поле '{field}': '{value}' не є датою (очікується РРРР-ММ-ДД)");
  }

  /// <summary>
  /// Дата й час у форматі ISO 8601. Зі зміщенням (Z, +03:00) - переводиться в UTC;
  /// без зміщення - вважається, що час уже в UTC.
  /// </summary>
  private static DateTime ParseDateTime(string field, string value)
  {
    if (DateTime.TryParseExact(value, LocalDateTimeFormats, CultureInfo.InvariantCulture,
          DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact))
      return exact;

    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
      return parsed.UtcDateTime;

    throw new DomainValidationException(
      $"Поле '{field}': '{value}' не є датою й часом (очікується ISO 8601, наприклад 2026-10-11T08:30:00Z)");
  }
}
