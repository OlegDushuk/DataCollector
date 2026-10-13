using DataCollector.WebUI.ServerApiServices;
using Microsoft.JSInterop;

namespace DataCollector.WebUI.Views.Pages;

/// <summary>Сторінка записів: експорт того, що відкрито (записи таблиці, зведення або дані діаграми).</summary>
public partial class Records
{
  private bool _isExporting;

  /// <summary>Повідомлення про успіх (файл збережено).</summary>
  private string? _notice;

  private string ExportCaption => _view switch
  {
    RecordsView.Summary => "Зведення з поточними групуваннями, показниками й фільтрами",
    RecordsView.Chart => "Дані діаграми: точки осі X і значення ліній",
    _ => TableExportCaption
  };

  private string TableExportCaption => HasFilters
      ? $"Знайдені записи ({_totalCount:N0}), видимі колонки і сортування"
      : $"Усі записи ({_totalCount:N0}), видимі колонки і сортування";

  /// <summary>
  /// Експорт того, що відкрито: записи таблиці (умови, пошук, сортування, видимі колонки в їхньому порядку)
  /// або зведення (групування, показники, умови). Файл формує API, браузер отримує його через JS.
  /// </summary>
  private async Task Export(string format)
  {
    if (_config is null || _isExporting)
      return;

    _isExporting = true;
    _notice = null;
    Error = null;

    try
    {
      DownloadedFile? file = null;

      var ok = await Try(async () =>
      {
        file = _view switch
        {
          RecordsView.Summary => await RecordApi.ExportAggregate(
            _config.Id, format, ApiFilters(), _search,
            _summarySettings.ApiDimensions(_filterFields), _summarySettings.ApiMetrics(), _summarySettings.Order),
          RecordsView.Chart => await RecordApi.ExportAggregate(
            _config.Id, format, ApiFilters(), _search,
            _chartSettings.ApiDimensions(_filterFields), _chartSettings.ApiMetrics(), "keys"),
          _ => await RecordApi.ExportRecords(
            _config.Id, format, ApiFilters(), _search, _sortKey, _sortDescending,
            _columns?.Visible.Select(c => c.Key).ToList())
        };
      });

      if (!ok || file is null)
        return;

      using var stream = new MemoryStream(file.Content);
      using var reference = new DotNetStreamReference(stream);
      await JS.InvokeVoidAsync("dataCollector.downloadFile", file.FileName, file.ContentType, reference);

      if (file.Truncated)
        Error = $"У файл потрапили лише перші {file.RowCount:N0} записів - це ліміт одного експорту. Звузьте фільтри, щоб вивантажити решту.";
      else
        _notice = $"Файл {file.FileName} збережено" + (file.RowCount is { } rows ? $" ({rows:N0} рядків)" : "");
    }
    catch (JSException)
    {
      Error = "Браузер не зміг зберегти файл. Оновіть сторінку (Ctrl+F5) і спробуйте ще раз.";
    }
    finally
    {
      _isExporting = false;
    }
  }
}
