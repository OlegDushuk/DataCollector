namespace DataCollector.WebUI.ServerApiServices;

/// <summary>Файл, отриманий з API (експорт), - для передачі в браузер на збереження.</summary>
public sealed class DownloadedFile
{
  public required string FileName { get; init; }
  public required string ContentType { get; init; }
  public required byte[] Content { get; init; }

  /// <summary>Скільки рядків у файлі (з заголовка X-Export-Rows).</summary>
  public int? RowCount { get; init; }

  /// <summary>У файл потрапили не всі записи - спрацював ліміт експорту.</summary>
  public bool Truncated { get; init; }
}
