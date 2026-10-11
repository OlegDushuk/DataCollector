namespace DataCollector.Server.Domain.Entities;

/// <summary>
/// Правила (атрибути) поля у текстовому вигляді - так, як їх передає API або як вони лежать у БД.
/// Перевірка й перетворення на типізовані значення - у <see cref="EntityPropertyConfig.ApplyRules"/>.
/// </summary>
/// <remarks>
/// Які правила доступні для якого типу:
/// <list type="bullet">
///   <item>усі типи - <see cref="IsRequired"/>, <see cref="DefaultValue"/>;</item>
///   <item>Text - <see cref="MinLength"/>, <see cref="MaxLength"/>;</item>
///   <item>Number, Date, DateTime - <see cref="Min"/>, <see cref="Max"/>;</item>
///   <item>Enum - <see cref="Options"/> (обов'язково).</item>
/// </list>
/// </remarks>
public sealed record PropertyRules
{
  public static readonly PropertyRules None = new();

  /// <summary>Значення не може бути порожнім.</summary>
  public bool IsRequired { get; init; }

  /// <summary>Значення, яке отримує новий запис, якщо поле не передали.</summary>
  public string? DefaultValue { get; init; }

  /// <summary>Мінімальне значення (число або дата) включно.</summary>
  public string? Min { get; init; }

  /// <summary>Максимальне значення (число або дата) включно.</summary>
  public string? Max { get; init; }

  public int? MinLength { get; init; }
  public int? MaxLength { get; init; }

  /// <summary>Допустимі варіанти для поля-переліку.</summary>
  public IReadOnlyList<string>? Options { get; init; }
}
