using System.Text.Json;

namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Значення полів запису: ключ поля -> значення.
/// Значення приймаються як JSON (рядок, число, true/false або null),
/// щоб зовнішні системи могли надсилати дані у звичному вигляді.
/// Поля, яких немає в словнику, залишаються порожніми.
/// </summary>
public class CreateDataInstanceCommand
{
  public Dictionary<string, JsonElement> Values { get; set; } = [];
}
