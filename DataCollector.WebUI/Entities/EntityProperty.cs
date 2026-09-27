namespace DataCollector.WebUI.Entities;

/// <summary>
/// Не використовується: значення полів тепер зберігаються в <see cref="EntityInstance.Values"/>.
/// Файл можна видалити.
/// </summary>
[Obsolete("Використовуйте EntityInstance.Values")]
public class EntityProperty
{
  public string? Value { get; set; }
  public EntityPropertyConfig? Config { get; set; }
}
