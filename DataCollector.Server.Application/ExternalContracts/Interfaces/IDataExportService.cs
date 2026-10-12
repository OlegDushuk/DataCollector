using DataCollector.Server.Application.ExternalContracts.Models;

namespace DataCollector.Server.Application.ExternalContracts.Interfaces;

/// <summary>Вивантаження записів і зведень у файли (CSV, Excel, JSON, XML).</summary>
public interface IDataExportService
{
  /// <summary>Записує файл з записами в <paramref name="output"/>. Помилки валідації - до першого байта файлу.</summary>
  Task<ExportFileInfo> ExportRecords(
    string config,
    ExportRecordsCommand command,
    Stream output,
    CancellationToken cancellationToken = default);

  /// <summary>Записує файл зі зведенням (групи і показники) в <paramref name="output"/>.</summary>
  Task<ExportFileInfo> ExportAggregate(
    string config,
    ExportAggregateCommand command,
    Stream output,
    CancellationToken cancellationToken = default);
}
