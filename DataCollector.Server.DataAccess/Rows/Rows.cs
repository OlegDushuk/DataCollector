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

internal class MonitoringRuleRow
{
  public Guid Id { get; set; }
  public Guid ConfigId { get; set; }
  public string ConfigName { get; set; } = null!;
  public string Name { get; set; } = null!;
  public bool IsEnabled { get; set; }
  public string? Filters { get; set; }
  public string? Search { get; set; }
  public string Function { get; set; } = null!;
  public string? FieldKey { get; set; }
  public string TimeField { get; set; } = null!;
  public int? WindowMinutes { get; set; }
  public string Operator { get; set; } = null!;
  public decimal Threshold { get; set; }
  public int ConsecutiveChecks { get; set; }
  public int CheckIntervalSeconds { get; set; }
  public byte State { get; set; }
  public DateTime? StateSince { get; set; }
  public decimal? LastValue { get; set; }
  public DateTime? LastCheckedAt { get; set; }
  public string? LastError { get; set; }
  public byte? PendingState { get; set; }
  public int PendingCount { get; set; }
  public DateTime NextCheckAt { get; set; }
  public int Version { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
}

internal class NotificationRow
{
  public Guid Id { get; set; }
  public Guid RuleId { get; set; }
  public string RuleName { get; set; } = null!;
  public string ConfigName { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public byte FromState { get; set; }
  public byte ToState { get; set; }
  public decimal? Value { get; set; }
  public decimal Threshold { get; set; }
  public string Message { get; set; } = null!;
  public DateTime? ReadAt { get; set; }
  public byte DeliveryStatus { get; set; }
  public int DeliveryAttempts { get; set; }
  public DateTime? DeliveredAt { get; set; }
  public string? DeliveryError { get; set; }
}

internal class InstanceOwnerRow
{
  public Guid Id { get; set; }
  public Guid EntityConfigId { get; set; }
}

internal class UserRow
{
  public Guid Id { get; set; }
  public string Login { get; set; } = null!;
  public string DisplayName { get; set; } = null!;
  public byte Role { get; set; }
  public string PasswordHash { get; set; } = null!;
  public Guid SecurityStamp { get; set; }
  public bool IsActive { get; set; }
  public int FailedLoginCount { get; set; }
  public DateTime? LockoutEnd { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public DateTime? LastLoginAt { get; set; }
}

internal class ApiKeyRow
{
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;
  public string Prefix { get; set; } = null!;
  public byte[] KeyHash { get; set; } = null!;
  public byte Role { get; set; }
  public DateTime CreatedAt { get; set; }
  public Guid? CreatedByUserId { get; set; }
  public string? CreatedByName { get; set; }
  public DateTime? LastUsedAt { get; set; }
  public DateTime? RevokedAt { get; set; }
}
