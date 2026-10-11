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
  public Guid EntityInstanceId { get; set; }
  public Guid EntityPropertyConfigId { get; set; }

  // Заповнена лише колонка, що відповідає типу поля.
  public string? ValueText { get; set; }
  public decimal? ValueNumber { get; set; }
  public bool? ValueBool { get; set; }
  public DateTime? ValueDate { get; set; }
}
