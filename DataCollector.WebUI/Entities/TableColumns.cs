using System.Text.Json;

namespace DataCollector.WebUI.Entities;

/// <summary>Колонка таблиці записів: поле моделі або системна дата.</summary>
public sealed class TableColumn
{
  public string Key { get; init; } = string.Empty;
  public string Name { get; init; } = string.Empty;
  public bool IsSystem { get; init; }
  public bool IsVisible { get; set; }
}

/// <summary>
/// Які колонки показує таблиця записів і в якому порядку. Видимі поля моделі - це й проєкція запиту
/// (fields у POST .../records/query): сервер повертає значення лише цих полів.
/// Вибір зберігається в браузері окремо для кожної моделі.
/// </summary>
public sealed class TableColumns
{

  public Guid ConfigId { get; private set; }
  public List<TableColumn> Items { get; private set; } = [];

  public IEnumerable<TableColumn> Visible => Items.Where(c => c.IsVisible);

  /// <summary>Вибір відрізняється від стандартного (видимість або порядок).</summary>
  public bool IsCustomized => ToJson() != _defaultJson;

  /// <summary>Ключі видимих полів моделі для запиту; null - усі поля (нічого не приховано).</summary>
  public List<string>? ApiFields => Items.Any(c => !c.IsSystem && !c.IsVisible)
    ? Items.Where(c => c is { IsSystem: false, IsVisible: true }).Select(c => c.Key).ToList()
    : null;

  /// <summary>Ключ у <see cref="Services.BrowserStorage"/> (повний ключ у браузері - datacollector.columns.{id}).</summary>
  public static string StorageKey(Guid configId) => "columns." + configId;

  private string _defaultJson = string.Empty;

  /// <summary>
  /// Колонки для моделі: збережений вибір (JSON з браузера), доповнений новими полями моделі.
  /// Поля, яких у моделі вже немає, відкидаються. Без збереженого вибору - усі поля і дата створення.
  /// </summary>
  public static TableColumns For(EntityConfig config, string? savedJson) => For(config, Parse(savedJson));

  /// <inheritdoc cref="For(EntityConfig, string?)"/>
  public static TableColumns For(EntityConfig config, IReadOnlyList<TableColumnState>? saved)
  {
    var all = config.Properties
      .Select(p => new TableColumn { Key = p.Key, Name = p.Name, IsVisible = true })
      .Append(new TableColumn { Key = FilterField.CreatedAtKey, Name = "Створено", IsSystem = true, IsVisible = true })
      .Append(new TableColumn { Key = FilterField.UpdatedAtKey, Name = "Змінено", IsSystem = true, IsVisible = false })
      .ToList();

    var result = new TableColumns { ConfigId = config.Id, Items = all };
    result._defaultJson = result.ToJson();

    if (saved is null || saved.Count == 0)
      return result;

    var byKey = all.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
    var ordered = new List<TableColumn>();

    foreach (var state in saved)
    {
      if (state.Key is null || !byKey.Remove(state.Key, out var column))
        continue;

      column.IsVisible = state.Visible;
      ordered.Add(column);
    }

    // Поля, додані до моделі після збереження вибору, показуємо - інакше їх легко не помітити.
    // Ставимо перед системними датами, як у порядку за замовчуванням.
    foreach (var column in all.Where(c => byKey.ContainsKey(c.Key)))
    {
      var index = column.IsSystem ? ordered.Count : ordered.FindIndex(c => c.IsSystem);
      ordered.Insert(index < 0 ? ordered.Count : index, column);
    }

    // Хоч одна колонка має лишатися видимою.
    if (!ordered.Any(c => c.IsVisible))
      ordered.ForEach(c => c.IsVisible = !c.IsSystem || c.Key == FilterField.CreatedAtKey);

    result.Items = ordered;
    return result;
  }

  public string ToJson() => JsonSerializer.Serialize(ToStates());

  /// <summary>Видимість і порядок колонок для збереження (у браузері або в налаштуваннях віджета дашборду).</summary>
  public List<TableColumnState> ToStates() => Items.Select(c => new TableColumnState { Key = c.Key, Visible = c.IsVisible }).ToList();

  /// <summary>Показати або сховати колонку. Останню видиму сховати не можна.</summary>
  public bool Toggle(TableColumn column)
  {
    if (column.IsVisible && Items.Count(c => c.IsVisible) == 1)
      return false;

    column.IsVisible = !column.IsVisible;
    return true;
  }

  public bool Move(TableColumn column, int offset)
  {
    var from = Items.IndexOf(column);
    var to = from + offset;

    if (from < 0 || to < 0 || to >= Items.Count)
      return false;

    Items.RemoveAt(from);
    Items.Insert(to, column);
    return true;
  }

  public void ShowAll() => Items.ForEach(c => c.IsVisible = true);

  /// <summary>Порядок і видимість за замовчуванням.</summary>
  public void Reset(EntityConfig config) => Items = For(config, (string?)null).Items;

  private static List<TableColumnState>? Parse(string? json)
  {
    if (string.IsNullOrWhiteSpace(json))
      return null;

    try
    {
      return JsonSerializer.Deserialize<List<TableColumnState>>(json);
    }
    catch (JsonException)
    {
      // Пошкоджений або старий формат - просто беремо вибір за замовчуванням.
      return null;
    }
  }
}

/// <summary>Збережений стан колонки: ключ і видимість (порядок - порядком у списку).</summary>
public sealed class TableColumnState
{
  public string? Key { get; set; }
  public bool Visible { get; set; }
}
