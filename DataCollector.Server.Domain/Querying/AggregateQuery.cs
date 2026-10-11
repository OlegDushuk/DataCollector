using System.Globalization;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Querying;

public enum AggregateFunction
{
  /// <summary>Кількість записів (без поля) або заповнених значень поля.</summary>
  Count,
  Sum,
  Avg,
  Min,
  Max
}

/// <summary>Інтервал групування дат.</summary>
public enum DateInterval
{
  Hour,
  Day,

  /// <summary>Тиждень з понеділка.</summary>
  Week,
  Month,
  Quarter,
  Year
}

/// <summary>Порядок груп у результаті.</summary>
public enum AggregateOrder
{
  /// <summary>За значеннями групування: дати - хронологічно, переліки - у порядку варіантів.</summary>
  Keys,

  /// <summary>За першим показником, від більшого (топ-N).</summary>
  ValueDesc,

  ValueAsc
}

/// <summary>
/// Групування: поле, за значеннями якого записи об'єднуються в групи.
/// Дати групуються за інтервалом (день, місяць...), числа - за точним значенням або діапазонами кроку <see cref="Step"/>.
/// </summary>
public sealed class AggregateDimension
{
  public RecordField Field { get; }
  public DateInterval? Interval { get; }
  public decimal? Step { get; }

  public bool IsDate => IsDateField(Field);

  private AggregateDimension(RecordField field, DateInterval? interval, decimal? step)
  {
    Field = field;
    Interval = interval;
    Step = step;
  }

  public static AggregateDimension Create(RecordField field, DateInterval? interval = null, decimal? step = null)
  {
    if (IsDateField(field))
    {
      if (step != null)
        throw Error(field, "крок задається лише для числових полів, для дат - інтервал");

      interval ??= DateInterval.Day;

      if (interval == DateInterval.Hour && field.Type == PropertyDataType.Date)
        throw Error(field, "дату без часу не можна групувати за годинами");

      return new AggregateDimension(field, interval, null);
    }

    if (interval != null)
      throw Error(field, "інтервал задається лише для дат");

    if (step != null)
    {
      if (field.Type != PropertyDataType.Number)
        throw Error(field, "крок задається лише для числових полів");

      if (step <= 0)
        throw Error(field, "крок має бути більшим за нуль");
    }

    return new AggregateDimension(field, null, step);
  }

  /// <summary>
  /// Значення групи з БД -> значення для API: дати - підпис інтервалу ("2026-10", "2026-Q4"),
  /// числа - без зайвих нулів.
  /// </summary>
  public object? NormalizeKey(object? raw)
  {
    if (raw is null or DBNull)
      return null;

    if (Interval is { } interval && raw is DateTime start)
    {
      return interval switch
      {
        DateInterval.Hour => start.ToString("yyyy-MM-dd'T'HH':00'", CultureInfo.InvariantCulture),
        DateInterval.Month => start.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        DateInterval.Quarter => $"{start.Year}-Q{(start.Month - 1) / 3 + 1}",
        DateInterval.Year => start.Year.ToString(CultureInfo.InvariantCulture),
        _ => start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
      };
    }

    return raw is decimal number ? TrimZeros(number) : raw;
  }

  private static bool IsDateField(RecordField field)
    => field.SystemField != null || field.Type is PropertyDataType.Date or PropertyDataType.DateTime;

  private static DomainValidationException Error(RecordField field, string message)
    => new($"Групування за полем '{field.Name}': {message}");

  internal static decimal TrimZeros(decimal value) => value / 1.0000000000000000000000000000m;

  /// <summary>Фактичні межі значень у діапазоні з БД (MIN / MAX); null - групування не діапазон.</summary>
  public AggregateRange? NormalizeRange(object? min, object? max)
  {
    if (Step is null)
      return null;

    static decimal? Number(object? raw) => raw is decimal number ? TrimZeros(number) : null;
    return new AggregateRange(Number(min), Number(max));
  }
}

/// <summary>Показник, що рахується для кожної групи.</summary>
public sealed class AggregateMetric
{
  public AggregateFunction Function { get; }

  /// <summary>Поле; null лише для Count (кількість записів).</summary>
  public RecordField? Field { get; }

  private AggregateMetric(AggregateFunction function, RecordField? field)
  {
    Function = function;
    Field = field;
  }

  public static AggregateMetric Create(AggregateFunction function, RecordField? field)
  {
    if (function == AggregateFunction.Count)
      return new AggregateMetric(function, field);

    if (field is null)
      throw new DomainValidationException($"Показник {DisplayName(function)}: потрібно вказати поле");

    var allowed = function switch
    {
      AggregateFunction.Sum or AggregateFunction.Avg => field.Type == PropertyDataType.Number && field.SystemField is null,
      _ => field.SystemField != null || field.Type is PropertyDataType.Number or PropertyDataType.Date or PropertyDataType.DateTime
    };

    if (!allowed)
    {
      throw new DomainValidationException(
        $"Показник «{DisplayName(function)}» не можна порахувати для поля '{field.Name}' (тип не підходить)");
    }

    return new AggregateMetric(function, field);
  }

  /// <summary>Назва показника для відповіді API: "Сума: Ціна".</summary>
  public string Name => Field is null
    ? "Кількість записів"
    : Function == AggregateFunction.Count
      ? $"Заповнено: {Field.Name}"
      : $"{DisplayName(Function)}: {Field.Name}";

  /// <summary>Значення з БД -> значення для API відповідного типу.</summary>
  public object? NormalizeValue(object? raw)
  {
    if (raw is null or DBNull)
      return null;

    return raw switch
    {
      int or long => Convert.ToInt64(raw, CultureInfo.InvariantCulture),
      decimal number when Function == AggregateFunction.Avg => AggregateDimension.TrimZeros(Math.Round(number, 6)),
      decimal number => AggregateDimension.TrimZeros(number),
      DateTime date when Field?.Type == PropertyDataType.Date && Field.SystemField is null => DateOnly.FromDateTime(date),
      DateTime dateTime => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
      _ => raw
    };
  }

  public static string DisplayName(AggregateFunction function) => function switch
  {
    AggregateFunction.Count => "Кількість",
    AggregateFunction.Sum => "Сума",
    AggregateFunction.Avg => "Середнє",
    AggregateFunction.Min => "Мінімум",
    AggregateFunction.Max => "Максимум",
    _ => function.ToString()
  };
}

/// <summary>
/// Агрегатний запит: які записи брати (умови й пошук - як у <see cref="RecordQuery"/>),
/// як групувати і що рахувати в кожній групі.
/// </summary>
public sealed class AggregateQuery
{
  public const int MaxDimensions = 2;
  public const int MaxMetrics = 10;
  public const int MaxLimit = 10_000;

  public IReadOnlyList<RecordFilter> Filters { get; }
  public string? Search { get; }
  public IReadOnlyList<AggregateDimension> Dimensions { get; }
  public IReadOnlyList<AggregateMetric> Metrics { get; }
  public AggregateOrder Order { get; }
  public int Limit { get; }

  /// <summary>
  /// Часовий пояс для меж інтервалів дат з часом ("день" - за місцевим часом, а не UTC).
  /// null - UTC. На дати без часу не впливає.
  /// </summary>
  public TimeZoneInfo? TimeZone { get; }

  public AggregateQuery(
    IReadOnlyList<RecordFilter> filters,
    string? search,
    IReadOnlyList<AggregateDimension> dimensions,
    IReadOnlyList<AggregateMetric> metrics,
    AggregateOrder order,
    int limit,
    TimeZoneInfo? timeZone)
  {
    if (filters.Count > RecordQuery.MaxFilters)
      throw new DomainValidationException($"Не більше {RecordQuery.MaxFilters} умов фільтра в одному запиті");

    if (dimensions.Count > MaxDimensions)
      throw new DomainValidationException($"Групувати можна не більше ніж за {MaxDimensions} полями");

    if (metrics.Count is 0 or > MaxMetrics)
      throw new DomainValidationException($"Потрібно від 1 до {MaxMetrics} показників");

    if (limit is < 1 or > MaxLimit)
      throw new DomainValidationException($"Ліміт груп має бути від 1 до {MaxLimit}");

    search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    if (search?.Length > RecordQuery.MaxSearchLength)
      throw new DomainValidationException($"Рядок пошуку довший за {RecordQuery.MaxSearchLength} символів");

    Filters = filters;
    Search = search;
    Dimensions = dimensions;
    Metrics = metrics;
    Order = order;
    Limit = limit;
    TimeZone = timeZone;
  }

  /// <summary>
  /// Часовий пояс за ідентифікатором IANA ("Europe/Kyiv") або Windows ("FLE Standard Time"); порожній - UTC.
  /// </summary>
  public static TimeZoneInfo? ResolveTimeZone(string? id)
  {
    if (string.IsNullOrWhiteSpace(id) || string.Equals(id, "UTC", StringComparison.OrdinalIgnoreCase))
      return null;

    try
    {
      return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
    }
    catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
    {
      throw new DomainValidationException($"Невідомий часовий пояс '{id}'");
    }
  }
}

/// <summary>Рядок результату: значення групувань і показників у тому ж порядку, що в запиті.</summary>
/// <param name="Ranges">
/// Для кожного групування: фактичні мінімум і максимум значень у групі, якщо групування - діапазон числа
/// (крок), інакше null. Сам діапазон [від; від + крок) не показує, які значення в ньому є.
/// </param>
public sealed record AggregateRow(
  IReadOnlyList<object?> Keys,
  IReadOnlyList<object?> Values,
  IReadOnlyList<AggregateRange?>? Ranges = null);

/// <summary>Фактичні межі значень у групі-діапазоні.</summary>
public sealed record AggregateRange(decimal? Min, decimal? Max);

/// <param name="Truncated">Груп більше за ліміт - повернуто лише перші.</param>
public sealed record AggregateResult(IReadOnlyList<AggregateRow> Rows, bool Truncated);
