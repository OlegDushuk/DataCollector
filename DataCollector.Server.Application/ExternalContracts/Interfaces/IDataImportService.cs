using DataCollector.Server.Application.ExternalContracts.Models;

namespace DataCollector.Server.Application.ExternalContracts.Interfaces;

/// <summary>Імпорт записів моделі з файлу (CSV, Excel, JSON).</summary>
public interface IDataImportService
{
  /// <param name="config">Id або ключ моделі.</param>
  /// <param name="fileName">Назва файлу - за розширенням визначається формат.</param>
  Task<ImportResultDto> Import(
    string config,
    Stream file,
    string fileName,
    ImportRecordsOptions options,
    CancellationToken cancellationToken = default);
}
