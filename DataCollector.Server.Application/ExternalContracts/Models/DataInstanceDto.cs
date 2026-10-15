namespace DataCollector.Server.Application.ExternalContracts.Models;

public class DataInstanceDto
{
  public Guid Id { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }

  /// <summary>
  /// Версія запису (оптимістична конкурентність). Передайте її при зміні або видаленні -
  /// якщо запис відтоді змінили, сервер відповість 409 замість того, щоб перезаписати чужі зміни.
  /// </summary>
  public long Version { get; set; }

  /// <summary>
  /// Ключ поля -> типізоване значення: string (Text, Enum), decimal, bool, DateOnly (Date),
  /// DateTime в UTC (DateTime) або null.
  /// </summary>
  public Dictionary<string, object?> Values { get; set; } = [];
}
