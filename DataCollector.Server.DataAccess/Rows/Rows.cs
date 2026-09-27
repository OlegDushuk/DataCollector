namespace DataCollector.Server.DataAccess.Rows;

// Плоскі класи для мапінгу результатів процедур через Dapper.
// Доменні сутності мають приватні сетери та валідацію в конструкторах, тому мапимо через ці рядки.

internal class EntityConfigRow
{
  public Guid Id { get; set; }
  public string Key { get; set; } = null!;
  public string Name { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
}

internal class EntityPropertyConfigRow
{
  public Guid Id { get; set; }
  public Guid EntityConfigId { get; set; }
  public string Key { get; set; } = null!;
  public string Name { get; set; } = null!;
  public int DataType { get; set; }
  public DateTime CreatedAt { get; set; }
}

internal class EntityInstanceRow
{
  public Guid Id { get; set; }
  public Guid EntityConfigId { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
}

internal class EntityPropertyValueRow
{
  public Guid Id { get; set; }
  public Guid EntityInstanceId { get; set; }
  public Guid EntityPropertyConfigId { get; set; }
  public string? Value { get; set; }
  public DateTime CreatedAt { get; set; }
}
