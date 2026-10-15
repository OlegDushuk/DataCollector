using System.Text.Json;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Querying;
using ContractDataType = DataCollector.Server.Application.ExternalContracts.Enums.PropertyDataType;
using DomainDataType = DataCollector.Server.Domain.Enums.PropertyDataType;

namespace DataCollector.Server.Application.Services.Internal;

internal static class Mapping
{
  public static DomainDataType ToDomain(this ContractDataType type) => (DomainDataType)(int)type;
  public static ContractDataType ToContract(this DomainDataType type) => (ContractDataType)(int)type;

  public static EntityConfigDto ToDto(this EntityConfig config) => new()
  {
    Id = config.Id,
    Key = config.Key,
    Name = config.Name,
    CreatedAt = config.CreatedAt,
    Properties = config.Properties.Select(p => p.ToDto()).ToList()
  };

  public static EntityPropertyConfigDto ToDto(this EntityPropertyConfig property) => new()
  {
    Id = property.Id,
    Key = property.Key,
    Name = property.Name,
    DataType = property.Type.ToContract(),
    CreatedAt = property.CreatedAt,
    Rules = new PropertyRulesDto
    {
      IsRequired = property.IsRequired,
      DefaultValue = property.DefaultValue,
      Min = property.Min,
      Max = property.Max,
      MinLength = property.MinLength,
      MaxLength = property.MaxLength,
      Options = property.Options.Count > 0 ? property.Options.ToList() : null
    }
  };

  public static PropertyRules ToDomain(this PropertyRulesCommand? rules)
  {
    if (rules is null)
      return PropertyRules.None;

    return new PropertyRules
    {
      IsRequired = rules.IsRequired,
      DefaultValue = rules.DefaultValue?.ToRawValue("defaultValue"),
      Min = rules.Min?.ToRawValue("min"),
      Max = rules.Max?.ToRawValue("max"),
      MinLength = rules.MinLength,
      MaxLength = rules.MaxLength,
      Options = rules.Options
    };
  }

  /// <param name="fields">Проєкція: лише ці поля у values (null - усі поля моделі).</param>
  public static DataInstanceDto ToDto(this EntityInstance instance, IReadOnlyList<EntityPropertyConfig>? fields = null) => new()
  {
    Id = instance.Id,
    CreatedAt = instance.CreatedAt,
    UpdatedAt = instance.UpdatedAt,
    Version = instance.Version,
    Values = instance.Properties
      .Where(p => fields is null || fields.Any(f => f.Id == p.Config.Id))
      .ToDictionary(p => p.Config.Key, p => p.Value)
  };

  /// <summary>Коди операторів фільтра в API (без урахування регістру).</summary>
  private static readonly Dictionary<string, FilterOperator> FilterOperators = new(StringComparer.OrdinalIgnoreCase)
  {
    ["eq"] = FilterOperator.Equals,
    ["neq"] = FilterOperator.NotEquals,
    ["gt"] = FilterOperator.GreaterThan,
    ["gte"] = FilterOperator.GreaterOrEqual,
    ["lt"] = FilterOperator.LessThan,
    ["lte"] = FilterOperator.LessOrEqual,
    ["between"] = FilterOperator.Between,
    ["contains"] = FilterOperator.Contains,
    ["notContains"] = FilterOperator.NotContains,
    ["startsWith"] = FilterOperator.StartsWith,
    ["endsWith"] = FilterOperator.EndsWith,
    ["in"] = FilterOperator.In,
    ["notIn"] = FilterOperator.NotIn,
    ["isEmpty"] = FilterOperator.IsEmpty,
    ["isNotEmpty"] = FilterOperator.IsNotEmpty
  };

  private static readonly Dictionary<string, AggregateFunction> AggregateFunctions = new(StringComparer.OrdinalIgnoreCase)
  {
    ["count"] = AggregateFunction.Count,
    ["sum"] = AggregateFunction.Sum,
    ["avg"] = AggregateFunction.Avg,
    ["min"] = AggregateFunction.Min,
    ["max"] = AggregateFunction.Max
  };

  private static readonly Dictionary<string, DateInterval> DateIntervals = new(StringComparer.OrdinalIgnoreCase)
  {
    ["hour"] = DateInterval.Hour,
    ["day"] = DateInterval.Day,
    ["week"] = DateInterval.Week,
    ["month"] = DateInterval.Month,
    ["quarter"] = DateInterval.Quarter,
    ["year"] = DateInterval.Year
  };

  private static readonly Dictionary<string, AggregateOrder> AggregateOrders = new(StringComparer.OrdinalIgnoreCase)
  {
    ["keys"] = AggregateOrder.Keys,
    ["valueDesc"] = AggregateOrder.ValueDesc,
    ["valueAsc"] = AggregateOrder.ValueAsc
  };

  public static AggregateFunction ToAggregateFunction(this string? code)
    => Parse(AggregateFunctions, code, "Невідома функція показника");

  public static DateInterval? ToDateInterval(this string? code)
    => string.IsNullOrWhiteSpace(code) ? null : Parse(DateIntervals, code, "Невідомий інтервал дат");

  public static AggregateOrder ToAggregateOrder(this string? code)
    => string.IsNullOrWhiteSpace(code) ? AggregateOrder.Keys : Parse(AggregateOrders, code, "Невідомий порядок груп");

  private static T Parse<T>(Dictionary<string, T> codes, string? code, string error)
  {
    if (code != null && codes.TryGetValue(code.Trim(), out var value))
      return value;

    throw new DomainValidationException($"{error} '{code}'. Доступні: {string.Join(", ", codes.Keys)}");
  }

  private static string Code<T>(Dictionary<string, T> codes, T value) where T : struct, Enum
    => codes.First(c => c.Value.Equals(value)).Key;

  public static AggregateResultDto ToDto(this AggregateQuery query, AggregateResult result) => new()
  {
    Dimensions = query.Dimensions.Select(d => new AggregateDimensionInfoDto
    {
      Field = FieldKey(d.Field),
      Name = d.Field.Name,
      DataType = d.Field.Type.ToContract(),
      Interval = d.Interval is { } interval ? Code(DateIntervals, interval) : null,
      Step = d.Step
    }).ToList(),
    Metrics = query.Metrics.Select(m => new AggregateMetricInfoDto
    {
      Function = Code(AggregateFunctions, m.Function),
      Field = m.Field is null ? null : FieldKey(m.Field),
      Name = m.Name
    }).ToList(),
    Rows = result.Rows.Select(r => new AggregateRowDto
    {
      Keys = [.. r.Keys],
      Values = [.. r.Values],
      Ranges = r.Ranges?.Select(x => x is null ? null : new AggregateRangeDto { Min = x.Min, Max = x.Max }).ToList()
    }).ToList(),
    Truncated = result.Truncated
  };

  private static string FieldKey(RecordField field) => field.Property?.Key ?? field.SystemField switch
  {
    RecordSystemField.UpdatedAt => RecordField.UpdatedAtKey,
    _ => RecordField.CreatedAtKey
  };

  public static FilterOperator ToFilterOperator(this string? code)
  {
    if (code != null && FilterOperators.TryGetValue(code.Trim(), out var op))
      return op;

    throw new DomainValidationException(
      $"Невідомий оператор фільтра '{code}'. Доступні: {string.Join(", ", FilterOperators.Keys)}");
  }

  /// <summary>
  /// Перетворює JSON-значення з запиту на рядок; доменна модель далі перевіряє відповідність типу поля.
  /// </summary>
  public static string? ToRawValue(this JsonElement element, string key)
  {
    return element.ValueKind switch
    {
      JsonValueKind.Null or JsonValueKind.Undefined => null,
      JsonValueKind.String => element.GetString(),
      JsonValueKind.Number => element.GetRawText(),
      JsonValueKind.True => "true",
      JsonValueKind.False => "false",
      _ => throw new DomainValidationException($"Поле '{key}': об'єкти та масиви не підтримуються")
    };
  }
}
