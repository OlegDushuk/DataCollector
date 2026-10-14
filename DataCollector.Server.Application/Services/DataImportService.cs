using System.Globalization;
using System.Text.RegularExpressions;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Import;
using DataCollector.Server.Application.Interfaces.Caching;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Querying;
using Microsoft.Extensions.Logging;

namespace DataCollector.Server.Application.Services;

/// <summary>
/// Імпорт записів з файлів CSV, Excel і JSON - зокрема з файлів, отриманих експортом.
/// </summary>
/// <remarks>
/// Колонки зіставляються з полями за ключем або назвою поля (як у заголовках експорту), без урахування регістру.
/// Кожне значення проходить ті самі перевірки, що й при введенні у формі чи через API (тип, обов'язковість,
/// межі, варіанти списку). Спершу перевіряється весь файл, і лише потім усе записується однією транзакцією:
/// або імпортуються всі правильні рядки, або (якщо є помилки і їх не дозволено пропускати) - нічого.
/// </remarks>
public partial class DataImportService(
  IEntityConfigCache entityConfigs,
  IEntityInstanceRepository entityInstanceRepository,
  ILogger<DataImportService> logger
) : IDataImportService
{
  /// <summary>Найбільше рядків в одному файлі - як і в експорті.</summary>
  public const int MaxRows = DataExportService.MaxRows;

  /// <summary>Скільки помилок повертати (решта лише рахується).</summary>
  public const int MaxReportedErrors = 200;

  private const string IdKey = "id";
  private static readonly string[] CreatedAtHeaders = [RecordField.CreatedAtKey, "Створено", "Дата створення"];
  private static readonly string[] UpdatedAtHeaders = [RecordField.UpdatedAtKey, "Змінено", "Дата зміни"];

  private static readonly string[] LocalDateTimeFormats =
  [
    "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss",
    "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "d.M.yyyy H:mm", "d.M.yyyy H:mm:ss", "yyyy-MM-dd", "dd.MM.yyyy"
  ];

  /// <summary>Зміщення часового поясу в кінці дати: Z, +03:00, -0500.</summary>
  [GeneratedRegex(@"(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase)]
  private static partial Regex OffsetSuffix();

  public async Task<ImportResultDto> Import(
    string config,
    Stream file,
    string fileName,
    ImportRecordsOptions options,
    CancellationToken cancellationToken = default)
  {
    var entity = await entityConfigs.Get(config);
    var format = ImportFormats.Detect(fileName, options.Format);
    var timeZone = AggregateQuery.ResolveTimeZone(options.TimeZone);

    var table = format.Reader().Read(file, MaxRows);
    var columns = MapColumns(entity, table.Headers, options);

    var result = new ImportResultDto
    {
      Format = format.ToString().ToLowerInvariant(),
      DryRun = options.DryRun,
      TotalRows = table.Rows.Count,
      Columns = columns.Select(c => c.ToDto()).ToList()
    };

    if (columns.All(c => c.Target is not (ColumnTarget.Field or ColumnTarget.Id)))
      throw new DomainValidationException(
        "Жодна колонка файлу не відповідає полям моделі - заголовки мають збігатися з ключами або назвами полів");

    var idColumn = columns.FirstOrDefault(c => c.Target == ColumnTarget.Id);
    var owners = idColumn is null
      ? []
      : await entityInstanceRepository.GetOwners(CollectIds(table, idColumn.Index), cancellationToken);

    var created = new List<EntityInstance>();
    var updated = new List<EntityInstance>();
    var seenIds = new HashSet<Guid>();

    foreach (var row in table.Rows)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var errors = new List<ImportErrorDto>();
      var instance = BuildInstance(entity, row, columns, idColumn, owners, seenIds, timeZone, errors, out var isUpdate);

      if (errors.Count > 0 || instance is null)
      {
        result.Invalid++;
        result.ErrorCount += errors.Count;
        foreach (var error in errors)
        {
          if (result.Errors.Count < MaxReportedErrors)
            result.Errors.Add(error);
        }

        continue;
      }

      (isUpdate ? updated : created).Add(instance);
    }

    result.Created = created.Count;
    result.Updated = updated.Count;

    var blocked = result.Invalid > 0 && !options.SkipInvalid;
    if (blocked)
    {
      // Файл імпортується цілком або ніяк: показуємо, що було б записано, але не записуємо.
      logger.LogInformation(
        "Імпорт у {Model} не виконано: {Invalid} з {Total} рядків мають помилки ({File})",
        entity.Key, result.Invalid, result.TotalRows, fileName);
    }

    if (options.DryRun || blocked || created.Count + updated.Count == 0)
      return result;

    var updatedProperties = columns
      .Where(c => c is { Target: ColumnTarget.Field, Property: not null })
      .Select(c => c.Property!.Id)
      .ToList();

    await entityInstanceRepository.Import(entity, created, updated, updatedProperties, cancellationToken);
    result.Applied = true;

    logger.LogInformation(
      "Імпорт {Format} {File} у {Model}: створено {Created}, оновлено {Updated}, пропущено {Invalid} рядків",
      result.Format, fileName, entity.Key, result.Created, result.Updated, result.Invalid);

    return result;
  }

  private static EntityInstance? BuildInstance(
    EntityConfig entity,
    ImportRow row,
    List<ImportColumn> columns,
    ImportColumn? idColumn,
    Dictionary<Guid, Guid> owners,
    HashSet<Guid> seenIds,
    TimeZoneInfo? timeZone,
    List<ImportErrorDto> errors,
    out bool isUpdate)
  {
    isUpdate = false;
    Guid? id = null;

    if (idColumn != null && Cell(row, idColumn.Index) is { } idText)
    {
      if (!Guid.TryParse(idText, out var parsed))
      {
        errors.Add(Error(row, idColumn, $"'{idText}' не є ідентифікатором запису (GUID)"));
        return null;
      }

      if (!seenIds.Add(parsed))
      {
        errors.Add(Error(row, idColumn, "Запис з цим id уже є вище у файлі"));
        return null;
      }

      if (owners.TryGetValue(parsed, out var owner) && owner != entity.Id)
      {
        errors.Add(Error(row, idColumn, "Запис з цим id належить іншій моделі"));
        return null;
      }

      id = parsed;
      isUpdate = owners.ContainsKey(parsed);
    }

    DateTime? createdAt = null;
    var createdAtColumn = columns.FirstOrDefault(c => c.Target == ColumnTarget.CreatedAt);
    if (!isUpdate && createdAtColumn != null && Cell(row, createdAtColumn.Index) is { } createdText)
    {
      createdAt = ParseUtc(createdText, timeZone);
      if (createdAt is null)
        errors.Add(Error(row, createdAtColumn, $"'{createdText}' не є датою й часом"));
      else if (createdAt > DateTime.UtcNow.AddMinutes(5))
        errors.Add(Error(row, createdAtColumn, "Дата створення не може бути в майбутньому"));
    }

    // Наявний запис оновлюється лише колонками файлу: значення інших полів не змінюються.
    var instance = isUpdate ? new EntityInstance(entity, id) : entity.CreateInstance(id, createdAt);

    foreach (var column in columns)
    {
      if (column is not { Target: ColumnTarget.Field, Property: { } property })
        continue;

      var value = Normalize(property, Cell(row, column.Index), timeZone);

      // Порожня комірка нового запису - «не задано»: лишається значення за замовчуванням.
      // У наявному записі порожня комірка очищає поле (файл - джерело правди для своїх колонок).
      if (value is null && !isUpdate)
        continue;

      try
      {
        instance.SetPropertyValue(property.Key, value);
      }
      catch (DomainValidationException e)
      {
        errors.Add(Error(row, column, e.Message));
      }
    }

    if (!isUpdate && errors.Count == 0)
    {
      try
      {
        instance.EnsureRequiredFilled();
      }
      catch (DomainValidationException e)
      {
        errors.Add(new ImportErrorDto { Row = row.Number, Message = e.Message });
      }
    }

    if (isUpdate)
      instance.MarkUpdated();

    return errors.Count == 0 ? instance : null;
  }

  /// <summary>
  /// Значення з файлу -> текст для доменної перевірки: знімає захист від формул з експорту CSV ('=...),
  /// дату з часом без зміщення вважає часом у вибраному поясі.
  /// </summary>
  private static string? Normalize(EntityPropertyConfig property, string? value, TimeZoneInfo? timeZone)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    if (property.Type is PropertyDataType.Text or PropertyDataType.Enum)
      return value.Length > 1 && value[0] == '\'' && value[1] is '=' or '+' or '-' or '@' or '\t' or '\r' ? value[1..] : value;

    if (property.Type == PropertyDataType.DateTime && timeZone != null && !OffsetSuffix().IsMatch(value.Trim()))
    {
      var utc = ParseUtc(value, timeZone);
      return utc?.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture) ?? value;
    }

    return value;
  }

  /// <summary>Дата з часом: зі зміщенням - як є, без зміщення - у поясі <paramref name="timeZone"/> (або UTC).</summary>
  private static DateTime? ParseUtc(string value, TimeZoneInfo? timeZone)
  {
    value = value.Trim();

    if (OffsetSuffix().IsMatch(value) &&
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var withOffset))
      return withOffset.UtcDateTime;

    if (!DateTime.TryParseExact(value, LocalDateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) &&
        !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
      return null;

    local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

    try
    {
      return timeZone is null
        ? DateTime.SpecifyKind(local, DateTimeKind.Utc)
        : TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
    catch (ArgumentException)
    {
      // Час, якого не існує в поясі (перехід на літній час) - зсуваємо на годину вперед.
      return TimeZoneInfo.ConvertTimeToUtc(local.AddHours(1), timeZone!);
    }
  }

  private static List<ImportColumn> MapColumns(EntityConfig entity, IReadOnlyList<string> headers, ImportRecordsOptions options)
  {
    var columns = new List<ImportColumn>();
    var used = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < headers.Count; i++)
    {
      var header = headers[i].Trim().Trim('﻿');

      if (header.Length == 0)
      {
        columns.Add(new ImportColumn(i, $"Колонка {i + 1}", ColumnTarget.Ignored, Note: "без заголовка"));
        continue;
      }

      var property = entity.FindProperty(header)
                     ?? entity.Properties.FirstOrDefault(p => string.Equals(p.Name.Trim(), header, StringComparison.OrdinalIgnoreCase));

      ImportColumn column;
      if (property != null)
        column = new ImportColumn(i, header, ColumnTarget.Field, property);
      else if (string.Equals(header, IdKey, StringComparison.OrdinalIgnoreCase))
        column = options.UpdateExisting
          ? new ImportColumn(i, header, ColumnTarget.Id)
          : new ImportColumn(i, header, ColumnTarget.Ignored, Note: "id не використовується - усі рядки стають новими записами");
      else if (CreatedAtHeaders.Contains(header, StringComparer.OrdinalIgnoreCase))
        column = options.KeepCreatedAt
          ? new ImportColumn(i, header, ColumnTarget.CreatedAt)
          : new ImportColumn(i, header, ColumnTarget.Ignored, Note: "дата створення - час імпорту");
      else if (UpdatedAtHeaders.Contains(header, StringComparer.OrdinalIgnoreCase))
        column = new ImportColumn(i, header, ColumnTarget.Ignored, Note: "дата зміни ставиться автоматично");
      else
        column = new ImportColumn(i, header, ColumnTarget.Ignored, Note: "немає такого поля в моделі");

      // Дві колонки для одного поля - неясно, яке значення брати.
      var target = column.Property?.Key ?? (column.Target is ColumnTarget.Id or ColumnTarget.CreatedAt ? column.Target.ToString() : null);
      if (target != null && !used.TryAdd(target, header))
        throw new DomainValidationException($"Колонки '{used[target]}' і '{header}' відповідають одному полю - залиште одну");

      columns.Add(column);
    }

    return columns;
  }

  private static List<Guid> CollectIds(ImportTable table, int index)
  {
    var ids = new HashSet<Guid>();
    foreach (var row in table.Rows)
    {
      if (Guid.TryParse(Cell(row, index), out var id))
        ids.Add(id);
    }

    return ids.ToList();
  }

  private static string? Cell(ImportRow row, int index)
  {
    var value = index < row.Cells.Count ? row.Cells[index] : null;
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
  }

  private static ImportErrorDto Error(ImportRow row, ImportColumn column, string message)
    => new() { Row = row.Number, Column = column.Header, Message = message };

  private enum ColumnTarget
  {
    Field,
    Id,
    CreatedAt,
    Ignored
  }

  private sealed record ImportColumn(
    int Index,
    string Header,
    ColumnTarget Target,
    EntityPropertyConfig? Property = null,
    string? Note = null)
  {
    public ImportColumnDto ToDto() => new()
    {
      Header = Header,
      Target = Target switch
      {
        ColumnTarget.Field => "field",
        ColumnTarget.Id => "id",
        ColumnTarget.CreatedAt => "createdAt",
        _ => "ignored"
      },
      FieldKey = Property?.Key,
      FieldName = Property?.Name,
      Note = Note
    };
  }
}
