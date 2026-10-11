using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Querying;

/// <summary>
/// Запит записів моделі: умови (поєднуються через І), текстовий пошук, сортування і сторінка.
/// </summary>
public sealed class RecordQuery
{
  public const int MaxFilters = 20;
  public const int MaxSearchLength = 200;

  public IReadOnlyList<RecordFilter> Filters { get; }

  /// <summary>
  /// Підрядок, який шукається в усіх текстових полях і полях-переліках (достатньо збігу в одному).
  /// </summary>
  public string? Search { get; }

  public RecordField SortField { get; }
  public bool SortDescending { get; }
  public int PageNumber { get; }
  public int PageSize { get; }

  public RecordQuery(
    IReadOnlyList<RecordFilter> filters,
    string? search,
    RecordField sortField,
    bool sortDescending,
    int pageNumber,
    int pageSize)
  {
    if (filters.Count > MaxFilters)
      throw new DomainValidationException($"Не більше {MaxFilters} умов фільтра в одному запиті");

    search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    if (search?.Length > MaxSearchLength)
      throw new DomainValidationException($"Рядок пошуку довший за {MaxSearchLength} символів");

    if (pageNumber < 1)
      throw new DomainValidationException("Номер сторінки має бути не менше 1");

    if (pageSize < 1)
      throw new DomainValidationException("Розмір сторінки має бути не менше 1");

    Filters = filters;
    Search = search;
    SortField = sortField;
    SortDescending = sortDescending;
    PageNumber = pageNumber;
    PageSize = pageSize;
  }

  /// <summary>Чи бере поле участь у текстовому пошуку.</summary>
  public static bool IsSearchable(PropertyDataType type) => type is PropertyDataType.Text or PropertyDataType.Enum;
}
