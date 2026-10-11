namespace DataCollector.Server.Application.ExternalContracts.Models;

public class DataInstanceDto
{
  public Guid Id { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }

  /// <summary>
  /// Ключ поля -> типізоване значення: string (Text, Enum), decimal, bool, DateOnly (Date),
  /// DateTime в UTC (DateTime) або null.
  /// </summary>
  public Dictionary<string, object?> Values { get; set; } = [];
}
