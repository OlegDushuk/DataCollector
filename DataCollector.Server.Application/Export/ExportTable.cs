namespace DataCollector.Server.Application.Export;

/// <summary>Тип значень колонки - від нього залежить запис комірки (число, дата, текст...).</summary>
public enum ExportValueKind
{
  Text,
  Number,
  Boolean,

  /// <summary>Дата без часу (<see cref="DateOnly"/>).</summary>
  Date,

  /// <summary>Дата й час у вибраному часовому поясі (<see cref="DateTimeOffset"/>).</summary>
  DateTime
}

/// <param name="Key">Ключ для JSON/XML (ключ поля).</param>
/// <param name="Title">Заголовок для CSV/Excel (назва поля).</param>
/// <param name="IsTechnical">Лише для JSON/XML (наприклад, id запису): у таблицях для людей не потрібна.</param>
public sealed record ExportColumn(string Key, string Title, ExportValueKind Kind, bool IsTechnical = false);

/// <summary>
/// Дані для експорту в будь-якому форматі: колонки і рядки. Рядки - асинхронний потік,
/// тож записи читаються з БД частинами і не тримаються в пам'яті всі одразу.
/// </summary>
/// <remarks>
/// Значення в рядках уже приведені до типів експорту: string, decimal, long, bool, DateOnly,
/// DateTimeOffset (дати з часом - у вибраному часовому поясі) або null.
/// </remarks>
public sealed class ExportTable
{
  /// <summary>Назва набору (аркуш Excel, корінь XML/JSON), наприклад назва моделі.</summary>
  public required string Title { get; init; }

  /// <summary>Ключ моделі - для метаданих JSON/XML.</summary>
  public required string Model { get; init; }

  /// <summary>Що експортовано: "records" або "summary".</summary>
  public required string Kind { get; init; }

  public required IReadOnlyList<ExportColumn> Columns { get; init; }

  /// <summary>Рядки в порядку колонок <see cref="Columns"/>.</summary>
  public IAsyncEnumerable<object?[]> Rows { get; set; } = EmptyRows();

  public required DateTimeOffset ExportedAt { get; init; }

  /// <summary>Скільки рядків записано і чи обрізано за лімітом - відомо після проходу по <see cref="Rows"/>.</summary>
  public int RowCount { get; set; }
  public bool Truncated { get; set; }

  private static async IAsyncEnumerable<object?[]> EmptyRows()
  {
    await Task.CompletedTask;
    yield break;
  }
}

internal interface IExportWriter
{
  Task WriteAsync(ExportTable table, Stream output, CancellationToken cancellationToken);
}
