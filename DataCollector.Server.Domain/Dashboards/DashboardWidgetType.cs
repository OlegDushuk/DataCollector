namespace DataCollector.Server.Domain.Dashboards;

/// <summary>Що показує віджет дашборду. У БД і API - рядковий код (<see cref="DashboardWidgetTypes"/>).</summary>
public enum DashboardWidgetType
{
  /// <summary>Таблиця записів з вибраними колонками, фільтрами й сортуванням.</summary>
  Table,

  /// <summary>Зведення: групування і показники.</summary>
  Summary,

  /// <summary>Діаграма (лінія, стовпці, смуги, кільце, теплова карта).</summary>
  Chart,

  /// <summary>Одне число: показник (кількість, сума, середнє...) за умовами.</summary>
  Kpi
}

public static class DashboardWidgetTypes
{
  public static string Code(this DashboardWidgetType type) => type switch
  {
    DashboardWidgetType.Table => "table",
    DashboardWidgetType.Summary => "summary",
    DashboardWidgetType.Chart => "chart",
    _ => "kpi"
  };

  public static DashboardWidgetType? Parse(string? code) => code?.Trim().ToLowerInvariant() switch
  {
    "table" => DashboardWidgetType.Table,
    "summary" => DashboardWidgetType.Summary,
    "chart" => DashboardWidgetType.Chart,
    "kpi" => DashboardWidgetType.Kpi,
    _ => null
  };
}
