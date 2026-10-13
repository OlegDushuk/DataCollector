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

  public bool IsRequired { get; set; }
  public string? DefaultValue { get; set; }

  /// <summary>Правила, специфічні для типу поля, у JSON (див. <see cref="Common.PropertySettingsJson"/>).</summary>
  public string? Settings { get; set; }
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

internal class DashboardRow
{
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;
  public string? Description { get; set; }
  public int RefreshSeconds { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
}

internal class DashboardWidgetRow
{
  public Guid Id { get; set; }
  public Guid? ConfigId { get; set; }
  public string Type { get; set; } = null!;
  public string? Title { get; set; }
  public int X { get; set; }
  public int Y { get; set; }
  public int W { get; set; }
  public int H { get; set; }
  public string Settings { get; set; } = null!;
}
