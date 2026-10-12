using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Експорт записів з тими ж умовами, пошуком і сортуванням, що й у POST .../records/query.
/// </summary>
/// <example>
/// { "format": "xlsx", "filters": [ { "field": "status", "operator": "eq", "value": "Нове" } ],
///   "sortBy": "price", "sortDescending": true, "fields": ["customer", "price", "createdAt"], "timeZone": "Europe/Kyiv" }
/// </example>
public class ExportRecordsCommand
{
  /// <summary>"csv", "xlsx", "json" або "xml".</summary>
  public string Format { get; set; } = "xlsx";

  public List<RecordFilterDto> Filters { get; set; } = [];
  public string? Search { get; set; }
  public string? SortBy { get; set; }
  public bool SortDescending { get; set; } = true;

  /// <summary>
  /// Колонки файлу в потрібному порядку: ключі полів, "createdAt", "updatedAt".
  /// Не задано - усі поля моделі, дата створення і дата зміни. У JSON/XML завжди є ще "id" запису.
  /// </summary>
  public List<string>? Fields { get; set; }

  /// <summary>Часовий пояс (IANA або Windows) для дат з часом у файлі; за замовчуванням UTC.</summary>
  public string? TimeZone { get; set; }
}

/// <summary>
/// Експорт зведення: ті самі групування, показники й умови, що в POST .../records/aggregate.
/// </summary>
public class ExportAggregateCommand : AggregateDataInstancesCommand
{
  /// <summary>"csv", "xlsx", "json" або "xml".</summary>
  public string Format { get; set; } = "xlsx";

  public ExportAggregateCommand()
  {
    // У файл - усі групи (до максимуму), а не перші 1000, як на екрані.
    Limit = AggregateQuery.MaxLimit;
  }
}

/// <summary>Результат експорту: як назвати файл і скільки в ньому рядків.</summary>
public class ExportFileInfo
{
  public required string FileName { get; init; }
  public required string ContentType { get; init; }
  public int RowCount { get; init; }

  /// <summary>Рядків більше за ліміт експорту - у файлі лише перші.</summary>
  public bool Truncated { get; init; }
}
