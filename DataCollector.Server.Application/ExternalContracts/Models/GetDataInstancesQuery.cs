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
}
