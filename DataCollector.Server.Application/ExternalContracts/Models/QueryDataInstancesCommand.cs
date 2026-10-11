namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Пошук записів: умови фільтра (усі мають виконуватися), текстовий пошук, сортування і сторінка.
/// </summary>
/// <example>
/// { "filters": [ { "field": "price", "operator": "gte", "value": 100 },
///                { "field": "status", "operator": "in", "values": ["Нове", "Оплачене"] } ],
///   "search": "ТОВ", "sortBy": "price", "sortDescending": true, "pageNumber": 1, "pageSize": 20 }
/// </example>
public class QueryDataInstancesCommand
{
  public List<RecordFilterDto> Filters { get; set; } = [];

  /// <summary>Підрядок для пошуку в текстових полях і полях-переліках.</summary>
  public string? Search { get; set; }

  /// <summary>Ключ поля, "createdAt" (за замовчуванням) або "updatedAt".</summary>
  public string? SortBy { get; set; }
  public bool SortDescending { get; set; } = true;

  public int PageNumber { get; set; } = 1;
  public int PageSize { get; set; } = 10;
}
