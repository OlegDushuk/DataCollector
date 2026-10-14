namespace DataCollector.WebUI.Views.Pages;

/// <summary>Сторінка записів: імпорт з файлу (діалог ImportDialog).</summary>
public partial class Records
{
  private bool _isImportOpen;

  private void OpenImport() => _isImportOpen = true;

  /// <summary>Записи імпортовано: нові можуть бути будь-де за сортуванням - таблиця з першої сторінки.</summary>
  private Task OnImported() => RefreshData(toFirstPage: true);
}
