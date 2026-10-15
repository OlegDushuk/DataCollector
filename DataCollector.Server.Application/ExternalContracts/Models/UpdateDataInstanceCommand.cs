using System.Text.Json;

namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Оновлює лише ті поля, що передані у словнику. Щоб очистити поле, передайте null.
/// </summary>
public class UpdateDataInstanceCommand
{
  public Dictionary<string, JsonElement> Values { get; set; } = [];

  /// <summary>
  /// Версія запису, яку бачив клієнт (з GET або зі списку). Не збігається з поточною - 409.
  /// null - без перевірки. Замість поля можна передати заголовок If-Match.
  /// </summary>
  public long? Version { get; set; }
}
