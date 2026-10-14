using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Application.Import;

/// <summary>Формат файлу імпорту - ті самі, що дає експорт (крім XML).</summary>
public enum ImportFormat
{
  Csv,
  Xlsx,
  Json
}

/// <summary>Рядок файлу: номер (як його бачить людина - рядок у Excel/CSV або номер об'єкта в JSON) і комірки.</summary>
public sealed record ImportRow(int Number, IReadOnlyList<string?> Cells);

/// <summary>
/// Вміст файлу у вигляді таблиці рядків: заголовки колонок і значення як текст.
/// Типи розбирає вже доменна модель (ті самі правила, що й для форми та API).
/// </summary>
public sealed class ImportTable
{
  public required IReadOnlyList<string> Headers { get; init; }
  public required IReadOnlyList<ImportRow> Rows { get; init; }
}

internal interface IImportReader
{
  /// <param name="maxRows">Більше рядків - помилка (імпорт обмежений, як і експорт).</param>
  ImportTable Read(Stream input, int maxRows);
}

public static class ImportFormats
{
  /// <summary>Формат за розширенням файлу (.csv, .xlsx, .json) або явним кодом.</summary>
  public static ImportFormat Detect(string? fileName, string? format = null)
  {
    var code = !string.IsNullOrWhiteSpace(format) ? format.Trim().TrimStart('.') : Path.GetExtension(fileName ?? "").TrimStart('.');

    return code.ToLowerInvariant() switch
    {
      "csv" or "txt" => ImportFormat.Csv,
      "xlsx" => ImportFormat.Xlsx,
      "json" => ImportFormat.Json,
      "xls" => throw new DomainValidationException(
        "Старий формат Excel (.xls) не підтримується - збережіть файл як .xlsx або .csv"),
      _ => throw new DomainValidationException(
        $"Невідомий формат файлу '{fileName}'. Підтримуються CSV, Excel (.xlsx) і JSON")
    };
  }

  internal static IImportReader Reader(this ImportFormat format) => format switch
  {
    ImportFormat.Csv => new CsvImportReader(),
    ImportFormat.Xlsx => new XlsxImportReader(),
    _ => new JsonImportReader()
  };

  internal static DomainValidationException TooManyRows(int maxRows)
    => new($"У файлі більше {maxRows.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ')} рядків - розділіть його на частини");
}
