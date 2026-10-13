using System.Runtime.CompilerServices;
using DataCollector.Server.Application.Export;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Caching;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Application.Services.Internal;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Querying;

namespace DataCollector.Server.Application.Services;

/// <summary>
/// Експорт записів і зведень у файли. Записи беруться тим самим рушієм запитів, що й таблиця
/// (умови, пошук, сортування, проєкція), частинами по <see cref="BatchSize"/>, і одразу пишуться у файл.
/// </summary>
public class DataExportService(
  IEntityConfigCache entityConfigs,
  IEntityInstanceRepository entityInstanceRepository
) : IDataExportService
{
  /// <summary>Найбільше записів в одному файлі: більші вивантаження - через API частинами або фільтрами.</summary>
  public const int MaxRows = 100_000;

  /// <summary>Скільки записів читається з БД за раз.</summary>
  public const int BatchSize = 2_000;

  public async Task<ExportFileInfo> ExportRecords(
    string config,
    ExportRecordsCommand command,
    Stream output,
    CancellationToken cancellationToken = default)
  {
    var format = ExportFormats.Parse(command.Format);
    var entity = await entityConfigs.Get(config);
    var timeZone = AggregateQuery.ResolveTimeZone(command.TimeZone);

    var fields = ResolveColumns(entity, command.Fields);
    var columns = new List<ExportColumn> { new("id", "Id", ExportValueKind.Text, IsTechnical: true) };
    columns.AddRange(fields.Select(f => new ExportColumn(Key(f), f.Name, Kind(f))));

    // Усі перевірки (умови, сортування) - до запису першого байта: помилка має стати відповіддю 400, а не битим файлом.
    var filters = RecordQueries.Filters(entity, command.Filters);
    var sort = RecordQueries.Sort(entity, command.SortBy);
    var projection = fields.Select(f => f.Property).OfType<EntityPropertyConfig>().ToList();

    RecordQuery Page(int pageNumber) =>
      new(filters, command.Search, sort, command.SortDescending, pageNumber, BatchSize, projection);

    // Конструктор запиту перевіряє пошук і кількість умов - теж до початку файлу.
    _ = Page(1);

    var exportedAt = Now(timeZone);
    var table = new ExportTable
    {
      Title = entity.Name,
      Model = entity.Key,
      Kind = "records",
      Columns = columns,
      ExportedAt = exportedAt
    };

    // Рядки знають про таблицю, щоб відмітити кількість і обрізання за лімітом.
    table.Rows = RecordRows(entity, fields, Page, table, timeZone, cancellationToken);

    await format.Writer().WriteAsync(table, output, cancellationToken);

    return new ExportFileInfo
    {
      FileName = FileName(entity, "records", exportedAt, format),
      ContentType = format.ContentType(),
      RowCount = table.RowCount,
      Truncated = table.Truncated
    };
  }

  public async Task<ExportFileInfo> ExportAggregate(
    string config,
    ExportAggregateCommand command,
    Stream output,
    CancellationToken cancellationToken = default)
  {
    var format = ExportFormats.Parse(command.Format);
    var entity = await entityConfigs.Get(config);
    var query = RecordQueries.Aggregate(entity, command);

    var result = await entityInstanceRepository.Aggregate(entity, query, cancellationToken);

    var columns = new List<ExportColumn>();
    foreach (var dimension in query.Dimensions)
      columns.AddRange(DimensionColumns(dimension));
    columns.AddRange(query.Metrics.Select(MetricColumn));
    columns = Unique(columns);

    var exportedAt = Now(query.TimeZone);
    var rows = result.Rows.Select(row => SummaryRow(query, row)).ToList();

    var table = new ExportTable
    {
      Title = $"{entity.Name} - зведення",
      Model = entity.Key,
      Kind = "summary",
      Columns = columns,
      ExportedAt = exportedAt,
      Rows = Async(rows),
      RowCount = rows.Count,
      Truncated = result.Truncated
    };

    await format.Writer().WriteAsync(table, output, cancellationToken);

    return new ExportFileInfo
    {
      FileName = FileName(entity, "summary", exportedAt, format),
      ContentType = format.ContentType(),
      RowCount = rows.Count,
      Truncated = result.Truncated
    };
  }

  /// <summary>
  /// Колонки файлу записів: вибрані ключі в заданому порядку (без повторів) або всі поля + обидві дати.
  /// </summary>
  private static List<RecordField> ResolveColumns(EntityConfig entity, List<string>? keys)
  {
    if (keys is null)
    {
      return entity.Properties
        .Select(p => RecordField.Resolve(entity, p.Key))
        .Append(RecordField.Resolve(entity, RecordField.CreatedAtKey))
        .Append(RecordField.Resolve(entity, RecordField.UpdatedAtKey))
        .ToList();
    }

    var fields = keys
      .Select(key => RecordField.Resolve(entity, key))
      .DistinctBy(Key, StringComparer.OrdinalIgnoreCase)
      .ToList();

    return fields.Count > 0
      ? fields
      : throw new DomainValidationException("Для експорту потрібна хоча б одна колонка");
  }

  /// <summary>
  /// Записи частинами: кожна частина - окремий запит сторінки (той самий SQL, що й для таблиці).
  /// Порядок стабільний (сортування + дата створення + ключ), тож частини не перетинаються.
  /// </summary>
  private async IAsyncEnumerable<object?[]> RecordRows(
    EntityConfig entity,
    IReadOnlyList<RecordField> fields,
    Func<int, RecordQuery> page,
    ExportTable table,
    TimeZoneInfo? timeZone,
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var written = 0;

    for (var pageNumber = 1; ; pageNumber++)
    {
      var (items, totalCount) = await entityInstanceRepository.Query(entity, page(pageNumber), cancellationToken);

      foreach (var item in items)
      {
        if (written == MaxRows)
        {
          table.Truncated = true;
          yield break;
        }

        var row = new object?[fields.Count + 1];
        row[0] = item.Id.ToString();

        for (var i = 0; i < fields.Count; i++)
          row[i + 1] = RecordValue(item, fields[i], timeZone);

        written++;
        table.RowCount = written;
        yield return row;
      }

      if (items.Count < BatchSize || written >= totalCount)
        yield break;

      if (written == MaxRows)
      {
        table.Truncated = totalCount > MaxRows;
        yield break;
      }
    }
  }

  private static object? RecordValue(EntityInstance item, RecordField field, TimeZoneInfo? timeZone)
  {
    if (field.Property is not { } property)
    {
      return field.SystemField == RecordSystemField.UpdatedAt
        ? item.UpdatedAt is { } updatedAt ? Local(updatedAt, timeZone) : null
        : Local(item.CreatedAt, timeZone);
    }

    var value = item.Properties.FirstOrDefault(p => p.Config.Id == property.Id)?.Value;
    return Convert(value, timeZone);
  }

  /// <summary>Колонки групування: діапазон числа - дві колонки (від включно, до не включно).</summary>
  private static IEnumerable<ExportColumn> DimensionColumns(AggregateDimension dimension)
  {
    var key = Key(dimension.Field);
    var name = dimension.Field.Name;

    if (dimension.Interval is { } interval)
      return [new ExportColumn(key, $"{name} ({IntervalName(interval)})", ExportValueKind.Text)];

    if (dimension.Step != null)
    {
      return
      [
        new ExportColumn(key + "From", $"{name}: від (включно)", ExportValueKind.Number),
        new ExportColumn(key + "To", $"{name}: до (не включно)", ExportValueKind.Number)
      ];
    }

    return [new ExportColumn(key, name, Kind(dimension.Field))];
  }

  private static ExportColumn MetricColumn(AggregateMetric metric)
  {
    if (metric.Field is null)
      return new ExportColumn("count", metric.Name, ExportValueKind.Number);

    var function = metric.Function.ToString().ToLowerInvariant();
    var kind = metric.Function is AggregateFunction.Min or AggregateFunction.Max ? Kind(metric.Field) : ExportValueKind.Number;

    return new ExportColumn($"{function}_{Key(metric.Field)}", metric.Name, kind);
  }

  private static object?[] SummaryRow(AggregateQuery query, AggregateRow row)
  {
    var values = new List<object?>();

    for (var i = 0; i < query.Dimensions.Count; i++)
    {
      var dimension = query.Dimensions[i];
      var key = row.Keys[i];

      if (dimension.Step is { } step)
      {
        var from = key as decimal?;
        values.Add(from);
        values.Add(from + step);
      }
      else
      {
        values.Add(Convert(key, query.TimeZone));
      }
    }

    values.AddRange(row.Values.Select(v => Convert(v, query.TimeZone)));
    return values.ToArray();
  }

  /// <summary>Значення з домену -> тип експорту (див. <see cref="ExportTable"/>).</summary>
  private static object? Convert(object? value, TimeZoneInfo? timeZone) => value switch
  {
    null => null,
    DateTime dateTime => Local(dateTime, timeZone),
    int number => (long)number,
    double number => (decimal)number,
    _ => value
  };

  /// <summary>UTC -> час у вибраному поясі з його зміщенням (2026-10-11T14:30:00+03:00).</summary>
  private static DateTimeOffset Local(DateTime utc, TimeZoneInfo? timeZone)
  {
    var value = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    return timeZone is null ? value : TimeZoneInfo.ConvertTime(value, timeZone);
  }

  private static DateTimeOffset Now(TimeZoneInfo? timeZone) => Local(DateTime.UtcNow, timeZone);

  private static ExportValueKind Kind(RecordField field) => field.Property is null
    ? ExportValueKind.DateTime
    : field.Type switch
    {
      PropertyDataType.Number => ExportValueKind.Number,
      PropertyDataType.Boolean => ExportValueKind.Boolean,
      PropertyDataType.Date => ExportValueKind.Date,
      PropertyDataType.DateTime => ExportValueKind.DateTime,
      _ => ExportValueKind.Text
    };

  private static string Key(RecordField field) => field.Property?.Key ?? field.SystemField switch
  {
    RecordSystemField.UpdatedAt => RecordField.UpdatedAtKey,
    _ => RecordField.CreatedAtKey
  };

  private static string IntervalName(DateInterval interval) => interval switch
  {
    DateInterval.Hour => "година",
    DateInterval.Day => "день",
    DateInterval.Week => "тиждень з понеділка",
    DateInterval.Month => "місяць",
    DateInterval.Quarter => "квартал",
    _ => "рік"
  };

  /// <summary>Однакові ключі колонок (два показники з однаковою функцією й полем) - з номером: count, count2.</summary>
  private static List<ExportColumn> Unique(List<ExportColumn> columns)
  {
    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var result = new List<ExportColumn>();

    foreach (var column in columns)
    {
      var key = column.Key;
      for (var n = 2; !used.Add(key); n++)
        key = column.Key + n;

      result.Add(column with { Key = key });
    }

    return result;
  }

  /// <summary>Зведення вже в пам'яті (до 10 000 груп) - віддаємо його writer-у тим самим потоком рядків.</summary>
  private static async IAsyncEnumerable<object?[]> Async(IEnumerable<object?[]> rows)
  {
    foreach (var row in rows)
      yield return row;

    await Task.CompletedTask;
  }

  /// <summary>orders-records-20261011-1430.xlsx - ключ моделі латиницею, час у вибраному поясі.</summary>
  private static string FileName(EntityConfig entity, string kind, DateTimeOffset at, ExportFormat format)
    => $"{entity.Key}-{kind}-{at:yyyyMMdd-HHmm}.{format.Extension()}";
}
