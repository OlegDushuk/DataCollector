using System.Globalization;
using System.Text;
using System.Xml;

namespace DataCollector.Server.Application.Export;

/// <summary>Текстове представлення значень для CSV, JSON і XML - машиночитне, незалежне від мови.</summary>
internal static class ExportValues
{
  /// <summary>Число з крапкою, дата ISO 8601 (2026-10-11), дата з часом з поясом (2026-10-11T14:30:00+03:00).</summary>
  public static string? Text(object? value) => value switch
  {
    null => null,
    string text => text,
    decimal number => number.ToString(CultureInfo.InvariantCulture),
    long number => number.ToString(CultureInfo.InvariantCulture),
    bool flag => flag ? "true" : "false",
    DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    DateTimeOffset dateTime => dateTime.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
    _ => value.ToString()
  };

  /// <summary>
  /// Прибирає символи, заборонені в XML 1.0 (керівні символи), - інакше XML/XLSX був би пошкоджений.
  /// </summary>
  public static string XmlSafe(string text)
  {
    var isValid = true;
    foreach (var c in text)
    {
      if (!XmlConvert.IsXmlChar(c) && !char.IsSurrogate(c))
      {
        isValid = false;
        break;
      }
    }

    if (isValid)
      return text;

    var result = new StringBuilder(text.Length);
    foreach (var c in text)
    {
      if (XmlConvert.IsXmlChar(c) || char.IsSurrogate(c))
        result.Append(c);
    }

    return result.ToString();
  }
}
