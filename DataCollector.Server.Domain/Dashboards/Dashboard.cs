using DataCollector.Server.Domain.Common;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Dashboards;

/// <summary>
/// Дашборд - набір віджетів (таблиць, зведень, діаграм, показників) з різних моделей на одному екрані.
/// Віджети розміщуються в сітці на <see cref="GridColumns"/> колонок.
/// </summary>
public sealed class Dashboard
{
  public const int GridColumns = 12;
  public const int MaxWidgets = 24;
  public const int MaxDescriptionLength = 512;

  /// <summary>Допустимі інтервали автооновлення, секунди (0 - вимкнено).</summary>
  public static readonly IReadOnlyList<int> RefreshOptions = [0, 30, 60, 300, 900];

  public Guid Id { get; }
  public string Name { get; private set; }
  public string? Description { get; private set; }
  public int RefreshSeconds { get; private set; }
  public DateTime CreatedAt { get; }
  public DateTime? UpdatedAt { get; private set; }

  /// <summary>Версія в сховищі (оптимістична конкурентність); 0 - дашборд ще не збережено.</summary>
  public long Version { get; private set; }

  public IReadOnlyList<DashboardWidget> Widgets => _widgets;
  private readonly List<DashboardWidget> _widgets = [];

  public Dashboard(
    string name,
    string? description = null,
    int refreshSeconds = 0,
    IEnumerable<DashboardWidget>? widgets = null,
    Guid? id = null,
    DateTime? createdAt = null,
    DateTime? updatedAt = null,
    long version = 0)
  {
    Id = id ?? Guid.NewGuid();
    CreatedAt = createdAt ?? DateTime.UtcNow;
    UpdatedAt = updatedAt;
    Version = version;

    Name = Guard.Name(name, "дашборду");
    Description = NormalizeDescription(description);
    RefreshSeconds = NormalizeRefresh(refreshSeconds);

    if (widgets != null)
      SetWidgets(widgets);
  }

  /// <summary>Нові назва, опис, автооновлення і повний набір віджетів (збереження з редактора).</summary>
  public void Change(string name, string? description, int refreshSeconds, IEnumerable<DashboardWidget> widgets)
  {
    Name = Guard.Name(name, "дашборду");
    Description = NormalizeDescription(description);
    RefreshSeconds = NormalizeRefresh(refreshSeconds);
    SetWidgets(widgets);
    UpdatedAt = DateTime.UtcNow;
  }

  /// <summary>Нова версія після збереження (її видає сховище).</summary>
  public void SetVersion(long version)
  {
    Version = version;
  }

  /// <summary>Копія з новими Id дашборду й віджетів: «Назва (копія)».</summary>
  public Dashboard Copy()
  {
    var name = Name + " (копія)";
    if (name.Length > Guard.MaxNameLength)
      name = Name;

    return new Dashboard(name, Description, RefreshSeconds, _widgets.Select(w => w.Copy()));
  }

  private void SetWidgets(IEnumerable<DashboardWidget> widgets)
  {
    var list = widgets.ToList();

    if (list.Count > MaxWidgets)
      throw new DomainValidationException($"На дашборді може бути не більше {MaxWidgets} віджетів");

    if (list.GroupBy(w => w.Id).Any(g => g.Count() > 1))
      throw new DomainValidationException("Віджети дашборду повторюються");

    _widgets.Clear();
    _widgets.AddRange(list.OrderBy(w => w.Y).ThenBy(w => w.X));
  }

  private static string? NormalizeDescription(string? description)
  {
    var value = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    if (value?.Length > MaxDescriptionLength)
      throw new DomainValidationException($"Опис дашборду не може бути довшим за {MaxDescriptionLength} символів");

    return value;
  }

  private static int NormalizeRefresh(int seconds)
  {
    if (!RefreshOptions.Contains(seconds))
      throw new DomainValidationException(
        $"Автооновлення: {string.Join(", ", RefreshOptions)} секунд (0 - вимкнено)");

    return seconds;
  }
}
