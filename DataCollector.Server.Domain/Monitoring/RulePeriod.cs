using System.Globalization;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.Domain.Monitoring;

/// <summary>
/// Період, за записами якого рахується показник правила.
/// <list type="bullet">
/// <item>Ковзний: останні <see cref="Minutes"/> хвилин до моменту перевірки (<see cref="Minutes"/> = null - усі записи).</item>
/// <item>Календарний: поточна година, день, тиждень (з понеділка), місяць, квартал або рік у часовому поясі правила.</item>
/// </list>
/// <see cref="Offset"/> зсуває період назад: 1 - попередній (попередні 30 днів, попередній місяць), 12 місяців - той самий місяць
/// торік. <see cref="ToDate"/> обрізає зсунутий календарний період до того ж моменту, що й поточний: 5 числа о 10:00
/// «попередній місяць до того ж моменту» - з 1 числа минулого місяця до 5 числа 10:00.
/// </summary>
public sealed record RulePeriod(int? Minutes, DateInterval? Unit, int Offset = 0, bool ToDate = false)
{
  public const int MaxMinutes = 525_600;
  public const int MaxOffset = 100;

  /// <summary>Усі записи, без обмеження часу.</summary>
  public static readonly RulePeriod AllTime = new(null, null);

  public bool IsCalendar => Unit is not null;
  public bool IsAllTime => Unit is null && Minutes is null;

  /// <summary>Перевіряє період і зводить до канонічного вигляду (ToDate має сенс лише для зсунутого календарного).</summary>
  /// <param name="what">Для тексту помилки: «Вікно часу», «Період порівняння».</param>
  public RulePeriod Validate(string what)
  {
    if (Minutes is not null && Unit is not null)
      throw new DomainValidationException($"{what}: вкажіть або кількість хвилин, або календарний період");

    if (Minutes is { } minutes && (minutes < 1 || minutes > MaxMinutes))
      throw new DomainValidationException($"{what} - від 1 хвилини до {MaxMinutes / 1440} днів");

    if (Offset < 0 || Offset > MaxOffset)
      throw new DomainValidationException($"{what}: зсув - від 0 до {MaxOffset} періодів");

    if (IsAllTime && Offset > 0)
      throw new DomainValidationException($"{what}: період «за весь час» не можна зсунути");

    return this with { ToDate = ToDate && IsCalendar && Offset > 0 };
  }

  /// <summary>Межі періоду в UTC: [From; To). null - межі немає.</summary>
  public (DateTime? From, DateTime? To) Range(DateTime nowUtc, TimeZoneInfo? timeZone)
  {
    if (Unit is { } unit)
      return CalendarRange(unit, nowUtc, timeZone);

    if (Minutes is not { } minutes)
      return (null, null);

    // Поточне вікно відкрите праворуч (як і раніше): запис з часом у майбутньому теж потрапляє.
    var to = Offset == 0 ? (DateTime?)null : nowUtc.AddMinutes(-(double)minutes * Offset);
    return (nowUtc.AddMinutes(-(double)minutes * (Offset + 1)), to);
  }

  /// <summary>Умови за полем часу, що обмежують записи періодом (0-2 умови).</summary>
  public List<RecordFilter> Filters(RecordField timeField, DateTime nowUtc, TimeZoneInfo? timeZone)
  {
    var (from, to) = Range(nowUtc, timeZone);
    var filters = new List<RecordFilter>(2);

    if (from is { } f)
      filters.Add(RecordFilter.Create(timeField, FilterOperator.GreaterOrEqual, Iso(f)));
    if (to is { } t)
      filters.Add(RecordFilter.Create(timeField, FilterOperator.LessThan, Iso(t)));

    return filters;
  }

  /// <summary>Період текстом: «за 15 хв», «за попередній місяць до того ж моменту», «за весь час».</summary>
  public string Text()
  {
    if (Unit is { } unit)
    {
      var feminine = unit == DateInterval.Hour;
      var noun = UnitName(unit);
      var text = Offset switch
      {
        0 => $"за {(feminine ? "поточну" : "поточний")} {noun}",
        1 => $"за {(feminine ? "попередню" : "попередній")} {noun}",
        _ => $"за {noun}, {(feminine ? "що була" : "що був")} {Offset} {UnitAgo(unit, Offset)} тому"
      };

      return ToDate ? text + " до того ж моменту" : text;
    }

    if (Minutes is not { } m)
      return "за весь час";

    return Offset switch
    {
      0 => $"за {Length(m)}",
      1 => $"за попередні {Length(m)}",
      _ => $"за {Length(m)}, що закінчилися {Length((long)m * Offset)} тому"
    };
  }

  private static string Length(long minutes) => minutes switch
  {
    _ when minutes % 1440 == 0 => $"{minutes / 1440} дн.",
    _ when minutes % 60 == 0 => $"{minutes / 60} год",
    _ => $"{minutes} хв"
  };

  private (DateTime? From, DateTime? To) CalendarRange(DateInterval unit, DateTime nowUtc, TimeZoneInfo? timeZone)
  {
    var localNow = timeZone is null ? nowUtc : TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
    localNow = DateTime.SpecifyKind(localNow, DateTimeKind.Unspecified);

    var currentStart = Floor(localNow, unit);
    var start = Shift(currentStart, unit, -Offset);
    var end = Shift(start, unit, 1);

    // Той самий момент усередині зсунутого періоду (31 березня -> кінець лютого).
    if (ToDate && Offset > 0)
    {
      var cut = start + (localNow - currentStart);
      if (cut < end)
        end = cut;
    }

    return (ToUtc(start, timeZone), ToUtc(end, timeZone));
  }

  private static DateTime Floor(DateTime local, DateInterval unit) => unit switch
  {
    DateInterval.Hour => new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0),
    DateInterval.Day => local.Date,
    DateInterval.Week => local.Date.AddDays(-(((int)local.DayOfWeek + 6) % 7)),
    DateInterval.Month => new DateTime(local.Year, local.Month, 1),
    DateInterval.Quarter => new DateTime(local.Year, (local.Month - 1) / 3 * 3 + 1, 1),
    _ => new DateTime(local.Year, 1, 1)
  };

  private static DateTime Shift(DateTime local, DateInterval unit, int count) => unit switch
  {
    DateInterval.Hour => local.AddHours(count),
    DateInterval.Day => local.AddDays(count),
    DateInterval.Week => local.AddDays(7 * count),
    DateInterval.Month => local.AddMonths(count),
    DateInterval.Quarter => local.AddMonths(3 * count),
    _ => local.AddYears(count)
  };

  /// <summary>Місцевий час -> UTC. Час, якого не існує (перехід на літній час), зсувається на годину вперед.</summary>
  private static DateTime ToUtc(DateTime local, TimeZoneInfo? timeZone)
  {
    if (timeZone is null)
      return DateTime.SpecifyKind(local, DateTimeKind.Utc);

    if (timeZone.IsInvalidTime(local))
      local = local.AddHours(1);

    return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
  }

  private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

  private static string UnitName(DateInterval unit) => unit switch
  {
    DateInterval.Hour => "годину",
    DateInterval.Day => "день",
    DateInterval.Week => "тиждень",
    DateInterval.Month => "місяць",
    DateInterval.Quarter => "квартал",
    _ => "рік"
  };

  /// <summary>«2 місяці тому», «5 днів тому».</summary>
  private static string UnitAgo(DateInterval unit, int count)
  {
    var form = (count % 10, count % 100) switch
    {
      (1, not 11) => 0,
      (2 or 3 or 4, not (12 or 13 or 14)) => 1,
      _ => 2
    };

    string[] words = unit switch
    {
      DateInterval.Hour => ["годину", "години", "годин"],
      DateInterval.Day => ["день", "дні", "днів"],
      DateInterval.Week => ["тиждень", "тижні", "тижнів"],
      DateInterval.Month => ["місяць", "місяці", "місяців"],
      DateInterval.Quarter => ["квартал", "квартали", "кварталів"],
      _ => ["рік", "роки", "років"]
    };

    return words[form];
  }
}
