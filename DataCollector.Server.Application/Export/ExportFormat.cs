using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Application.Export;

public enum ExportFormat
{
  /// <summary>CSV (RFC 4180): кома, UTF-8 з BOM - для обміну з іншими системами.</summary>
  Csv,

  /// <summary>Excel (Office Open XML): типізовані комірки, дати, фільтр у заголовку.</summary>
  Xlsx,

  /// <summary>JSON: колонки і рядки-об'єкти з ключами полів.</summary>
  Json,

  /// <summary>XML: колонки і рядки з елементами за ключами полів.</summary>
  Xml
}

public static class ExportFormats
{
  private static readonly Dictionary<string, ExportFormat> Codes = new(StringComparer.OrdinalIgnoreCase)
  {
    ["csv"] = ExportFormat.Csv,
    ["xlsx"] = ExportFormat.Xlsx,
    ["json"] = ExportFormat.Json,
    ["xml"] = ExportFormat.Xml
  };

  public static ExportFormat Parse(string? code)
  {
    if (code != null && Codes.TryGetValue(code.Trim(), out var format))
      return format;

    throw new DomainValidationException(
      $"Невідомий формат експорту '{code}'. Доступні: {string.Join(", ", Codes.Keys)}");
  }

  public static string Extension(this ExportFormat format) => format switch
  {
    ExportFormat.Csv => "csv",
    ExportFormat.Xlsx => "xlsx",
    ExportFormat.Json => "json",
    _ => "xml"
  };

  public static string ContentType(this ExportFormat format) => format switch
  {
    ExportFormat.Csv => "text/csv; charset=utf-8",
    ExportFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    ExportFormat.Json => "application/json; charset=utf-8",
    _ => "application/xml; charset=utf-8"
  };

  internal static IExportWriter Writer(this ExportFormat format) => format switch
  {
    ExportFormat.Csv => new CsvExportWriter(),
    ExportFormat.Xlsx => new XlsxExportWriter(),
    ExportFormat.Json => new JsonExportWriter(),
    _ => new XmlExportWriter()
  };
}
