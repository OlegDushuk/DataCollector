using System.Text.Json;

namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Оновлює лише ті поля, що передані у словнику. Щоб очистити поле, передайте null.
/// </summary>
public class UpdateDataInstanceCommand
{
  public Dictionary<string, JsonElement> Values { get; set; } = [];
}
