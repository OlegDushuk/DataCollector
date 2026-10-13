using System.Text.Json;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Dashboards;

/// <summary>
/// Віджет дашборду: тип, модель-джерело, позиція в сітці на <see cref="Dashboard.GridColumns"/> колонок
/// і налаштування (фільтри, колонки, групування, вигляд) - JSON-документ, формат якого визначає інтерфейс.
/// </summary>
/// <remarks>
/// Сервер не розбирає налаштування: віджет отримує дані через ті самі ендпоінти записів і агрегації,
/// що й сторінка записів, і всі умови там перевіряються як звичайно. Тут лише гарантується,
/// що це JSON-об'єкт розумного розміру.
/// </remarks>
public sealed class DashboardWidget
{
  public const int MaxTitleLength = 128;
  public const int MaxHeight = 12;
  public const int MaxRow = 1000;

  /// <summary>Найбільший розмір налаштувань одного віджета (символів JSON).</summary>
  public const int MaxSettingsLength = 64 * 1024;

  public Guid Id { get; }
  public Guid? ConfigId { get; }
  public DashboardWidgetType Type { get; }
  public string? Title { get; }
  public int X { get; }
  public int Y { get; }
  public int W { get; }
  public int H { get; }
  public string Settings { get; }

  public DashboardWidget(
    Guid? id,
    Guid? configId,
    DashboardWidgetType type,
    string? title,
    int x, int y, int w, int h,
    string? settings)
  {
    Id = id is { } value && value != Guid.Empty ? value : Guid.NewGuid();
    ConfigId = configId == Guid.Empty ? null : configId;
    Type = type;

    Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    if (Title?.Length > MaxTitleLength)
      throw new DomainValidationException($"Назва віджета не може бути довшою за {MaxTitleLength} символів");

    if (w < 1 || w > Dashboard.GridColumns)
      throw new DomainValidationException($"Ширина віджета - від 1 до {Dashboard.GridColumns} колонок");

    if (x < 0 || x + w > Dashboard.GridColumns)
      throw new DomainValidationException($"Віджет виходить за межі сітки з {Dashboard.GridColumns} колонок");

    if (h < 1 || h > MaxHeight)
      throw new DomainValidationException($"Висота віджета - від 1 до {MaxHeight} рядів");

    if (y < 0 || y > MaxRow)
      throw new DomainValidationException("Некоректний ряд віджета");

    X = x;
    Y = y;
    W = w;
    H = h;
    Settings = NormalizeSettings(settings);
  }

  /// <summary>Копія віджета з новим Id (для дублювання дашборду).</summary>
  public DashboardWidget Copy() => new(null, ConfigId, Type, Title, X, Y, W, H, Settings);

  private static string NormalizeSettings(string? settings)
  {
    if (string.IsNullOrWhiteSpace(settings))
      return "{}";

    if (settings.Length > MaxSettingsLength)
      throw new DomainValidationException("Налаштування віджета завеликі");

    try
    {
      using var document = JsonDocument.Parse(settings);
      if (document.RootElement.ValueKind != JsonValueKind.Object)
        throw new DomainValidationException("Налаштування віджета мають бути JSON-об'єктом");
    }
    catch (JsonException)
    {
      throw new DomainValidationException("Налаштування віджета - некоректний JSON");
    }

    return settings;
  }
}
