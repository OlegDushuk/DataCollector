namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>Параметри імпорту записів з файлу.</summary>
public class ImportRecordsOptions
{
  /// <summary>Лише перевірити файл і показати, що буде зроблено, нічого не записуючи.</summary>
  public bool DryRun { get; set; }

  /// <summary>
  /// Рядок з колонкою id наявного запису цієї моделі оновлює його (лише колонки з файлу), інакше -
  /// створюється новий запис з цим id. false - id ігнорується, усі рядки стають новими записами.
  /// </summary>
  public bool UpdateExisting { get; set; } = true;

  /// <summary>
  /// Записати правильні рядки, пропустивши рядки з помилками. false - якщо є хоч одна помилка,
  /// не записується нічого (файл імпортується цілком або ніяк).
  /// </summary>
  public bool SkipInvalid { get; set; }

  /// <summary>Колонка «Створено» (createdAt) задає дату створення нових записів - для історичних даних.</summary>
  public bool KeepCreatedAt { get; set; } = true;

  /// <summary>Часовий пояс дат з часом без зміщення (як у файлі Excel); за замовчуванням UTC.</summary>
  public string? TimeZone { get; set; }

  /// <summary>Формат, якщо розширення файлу не вказує на нього: csv, xlsx, json.</summary>
  public string? Format { get; set; }
}

/// <summary>Що зроблено (або буде зроблено, якщо <see cref="DryRun"/>) під час імпорту.</summary>
public class ImportResultDto
{
  public string Format { get; set; } = null!;
  public bool DryRun { get; set; }

  /// <summary>Чи записано зміни в БД.</summary>
  public bool Applied { get; set; }

  /// <summary>Рядків даних у файлі (без заголовка і порожніх рядків).</summary>
  public int TotalRows { get; set; }

  public int Created { get; set; }
  public int Updated { get; set; }

  /// <summary>Рядків з помилками (не записані).</summary>
  public int Invalid { get; set; }

  /// <summary>Перші помилки (не більше 200); усього - <see cref="ErrorCount"/>.</summary>
  public List<ImportErrorDto> Errors { get; set; } = [];
  public int ErrorCount { get; set; }

  /// <summary>Як колонки файлу зіставлено з полями моделі.</summary>
  public List<ImportColumnDto> Columns { get; set; } = [];
}

public class ImportErrorDto
{
  /// <summary>Номер рядка у файлі (Excel/CSV - як у програмі, JSON - номер об'єкта).</summary>
  public int Row { get; set; }

  /// <summary>Заголовок колонки; null - помилка всього рядка.</summary>
  public string? Column { get; set; }

  public string Message { get; set; } = null!;
}

public class ImportColumnDto
{
  public string Header { get; set; } = null!;

  /// <summary>field - поле моделі, id - ідентифікатор запису, createdAt - дата створення, ignored - не імпортується.</summary>
  public string Target { get; set; } = null!;

  public string? FieldKey { get; set; }
  public string? FieldName { get; set; }

  /// <summary>Чому колонку пропущено (для ignored).</summary>
  public string? Note { get; set; }
}
