using System.Globalization;
using DataCollector.Server.Domain.Common;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Entities;

/// <summary>
/// Опис поля моделі: ключ, назва, тип даних і правила для значень.
/// </summary>
public class EntityPropertyConfig
{
  public const int MaxOptions = 200;

  public Guid Id { get; private set; }
  public string Key { get; private set; }
  public PropertyDataType Type { get; private set; }

  public DateTime CreatedAt { get; private set; }
  public string Name { get; private set; }

  /// <summary>Значення не може бути порожнім.</summary>
  public bool IsRequired { get; private set; }

  /// <summary>Типізоване значення за замовчуванням для нових записів (або null).</summary>
  public object? DefaultValue { get; private set; }

  /// <summary>Межі значення включно: decimal для Number, DateOnly для Date, DateTime для DateTime.</summary>
  public object? Min { get; private set; }
  public object? Max { get; private set; }

  /// <summary>Межі довжини тексту (лише Text).</summary>
  public int? MinLength { get; private set; }
  public int? MaxLength { get; private set; }

  /// <summary>Варіанти значень (лише Enum), у заданому порядку.</summary>
  public IReadOnlyList<string> Options { get; private set; } = [];

  public EntityPropertyConfig(
    string key,
    string name,
    PropertyDataType type,
    Guid? id = null,
    DateTime? createdAt = null,
    PropertyRules? rules = null)
  {
    if (!Enum.IsDefined(type))
      throw new DomainValidationException($"Невідомий тип поля '{type}'");

    Id = id ?? Guid.NewGuid();
    CreatedAt = createdAt ?? DateTime.UtcNow;
    Key = Guard.Key(key, "поля");
    Name = Guard.Name(name, "поля");
    Type = type;

    ApplyRules(rules ?? PropertyRules.None);
  }

  /// <summary>
  /// Замінює всі правила поля. Правила перевіряються повністю до зміни стану:
  /// або застосовуються всі, або (при помилці) не змінюється нічого.
  /// </summary>
  /// <remarks>
  /// Нові правила діють для наступних змін записів; уже збережені значення не перевіряються повторно.
  /// </remarks>
  public void ApplyRules(PropertyRules rules)
  {
    var hasRange = Type is PropertyDataType.Number or PropertyDataType.Date or PropertyDataType.DateTime;

    if (!hasRange && (!string.IsNullOrWhiteSpace(rules.Min) || !string.IsNullOrWhiteSpace(rules.Max)))
      throw Error("мінімум і максимум доступні лише для чисел і дат");

    if (Type != PropertyDataType.Text && (rules.MinLength != null || rules.MaxLength != null))
      throw Error("обмеження довжини доступні лише для текстових полів");

    if (Type != PropertyDataType.Enum && rules.Options is { Count: > 0 })
      throw Error("варіанти значень доступні лише для поля-переліку");

    var min = hasRange ? PropertyValueParser.Parse(Type, Name, rules.Min) : null;
    var max = hasRange ? PropertyValueParser.Parse(Type, Name, rules.Max) : null;

    if (min != null && max != null && Comparer<object>.Default.Compare(min, max) > 0)
      throw Error("мінімум більший за максимум");

    var (minLength, maxLength) = ValidateLength(rules.MinLength, rules.MaxLength);
    var options = Type == PropertyDataType.Enum ? ValidateOptions(rules.Options) : [];

    // Значення за замовчуванням перевіряється за новими правилами.
    var defaultValue = Check(
      PropertyValueParser.Parse(Type, Name, rules.DefaultValue),
      min, max, minLength, maxLength, options);

    IsRequired = rules.IsRequired;
    Min = min;
    Max = max;
    MinLength = minLength;
    MaxLength = maxLength;
    Options = options;
    DefaultValue = defaultValue;
  }

  /// <summary>
  /// Правила у текстовому вигляді (інваріантний формат) - для збереження в БД.
  /// </summary>
  public PropertyRules GetRules() => new()
  {
    IsRequired = IsRequired,
    DefaultValue = PropertyValueParser.ToInvariantString(DefaultValue),
    Min = PropertyValueParser.ToInvariantString(Min),
    Max = PropertyValueParser.ToInvariantString(Max),
    MinLength = MinLength,
    MaxLength = MaxLength,
    Options = Options.Count > 0 ? Options : null
  };

  /// <summary>
  /// Розбирає значення з тексту і перевіряє його за правилами поля. Порожнє значення -> null
  /// (обов'язковість перевіряє запис, бо вона залежить від того, створюється він чи оновлюється).
  /// </summary>
  public object? ParseValue(string? value)
  {
    var parsed = PropertyValueParser.Parse(Type, Name, value);
    return Check(parsed, Min, Max, MinLength, MaxLength, Options);
  }

  internal void ChangeKey(string newKey)
  {
    Key = Guard.Key(newKey, "поля");
  }

  internal void ChangeName(string newName)
  {
    Name = Guard.Name(newName, "поля");
  }

  /// <summary>
  /// Перевірка типізованого значення за правилами. Для Enum повертає варіант у тому написанні,
  /// в якому він заданий у правилах ("high" -> "High").
  /// </summary>
  private object? Check(
    object? value,
    object? min,
    object? max,
    int? minLength,
    int? maxLength,
    IReadOnlyList<string> options)
  {
    if (value is null)
      return null;

    if (min != null && Comparer<object>.Default.Compare(value, min) < 0)
      throw Error($"значення {Display(value)} менше за мінімум {Display(min)}");

    if (max != null && Comparer<object>.Default.Compare(value, max) > 0)
      throw Error($"значення {Display(value)} більше за максимум {Display(max)}");

    if (value is string text)
    {
      if (minLength != null && text.Length < minLength)
        throw Error($"довжина тексту менша за мінімальну ({minLength})");

      if (maxLength != null && text.Length > maxLength)
        throw Error($"довжина тексту більша за максимальну ({maxLength})");

      if (Type == PropertyDataType.Enum)
      {
        return options.FirstOrDefault(o => string.Equals(o, text, StringComparison.OrdinalIgnoreCase))
               ?? throw Error($"'{text}' немає серед варіантів: {string.Join(", ", options)}");
      }
    }

    return value;
  }

  private (int? MinLength, int? MaxLength) ValidateLength(int? minLength, int? maxLength)
  {
    if (minLength is < 0)
      throw Error("мінімальна довжина не може бути від'ємною");

    if (maxLength is < 1 or > PropertyValueParser.MaxTextLength)
      throw Error($"максимальна довжина має бути від 1 до {PropertyValueParser.MaxTextLength}");

    if (minLength > (maxLength ?? PropertyValueParser.MaxTextLength))
      throw Error("мінімальна довжина більша за максимальну");

    return (minLength is 0 ? null : minLength, maxLength);
  }

  private List<string> ValidateOptions(IReadOnlyList<string>? options)
  {
    var result = new List<string>();

    foreach (var option in options ?? [])
    {
      var value = option?.Trim();

      if (string.IsNullOrEmpty(value))
        continue;

      if (value.Length > Guard.MaxNameLength)
        throw Error($"варіант '{value[..20]}…' довший за {Guard.MaxNameLength} символів");

      if (result.Contains(value, StringComparer.OrdinalIgnoreCase))
        throw Error($"варіант '{value}' вказано кілька разів");

      result.Add(value);
    }

    if (result.Count == 0)
      throw Error("для переліку потрібно задати хоча б один варіант");

    if (result.Count > MaxOptions)
      throw Error($"не більше {MaxOptions} варіантів");

    return result;
  }

  private DomainValidationException Error(string message) => new($"Поле '{Name}': {message}");

  private static string Display(object value) => value switch
  {
    DateOnly date => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
    DateTime dateTime => dateTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC",
    _ => PropertyValueParser.ToInvariantString(value) ?? ""
  };
}
