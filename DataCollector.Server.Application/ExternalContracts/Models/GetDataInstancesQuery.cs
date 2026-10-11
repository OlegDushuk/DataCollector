namespace DataCollector.Server.Application.ExternalContracts.Models;

public class GetDataInstancesQuery
{
  public int PageNumber { get; set; } = 1;
  public int PageSize { get; set; } = 10;

  /// <summary>
  /// Ключ поля для сортування або "createdAt" (за замовчуванням).
  /// </summary>
  public string? SortBy { get; set; }
  public bool SortDescending { get; set; } = true;

  /// <summary>Підрядок для пошуку в текстових полях і полях-переліках. Умови фільтра - у POST .../records/query.</summary>
  public string? Search { get; set; }

  /// <summary>Ключі полів через кому, значення яких повернути (?fields=name,price). Не задано - усі поля.</summary>
  public string? Fields { get; set; }
}
