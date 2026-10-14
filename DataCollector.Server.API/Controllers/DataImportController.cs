using DataCollector.Server.API.Auth;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.Server.API.Controllers;

/// <summary>
/// Імпорт записів з файлу: CSV, Excel (xlsx), JSON - у тому числі з файлів експорту цієї ж системи.
/// </summary>
[ApiController]
[Route("api/data-configs/{config}/records")]
[Authorize(Policy = AuthPolicies.CanEdit)]
public class DataImportController(IDataImportService dataImportService) : ControllerBase
{
  /// <summary>Найбільший розмір файлу імпорту, байт.</summary>
  public const long MaxFileSize = 50L * 1024 * 1024;

  /// <summary>
  /// multipart/form-data: file - файл; dryRun, updateExisting, skipInvalid, keepCreatedAt, timeZone, format - параметри.
  /// Відповідь - звіт: скільки створено/оновлено, зіставлення колонок і помилки по рядках.
  /// </summary>
  /// <example>curl -H "X-Api-Key: dc_…" -F file=@orders.csv -F dryRun=true http://localhost:5054/api/data-configs/orders/records/import</example>
  [HttpPost("import")]
  [RequestSizeLimit(MaxFileSize)]
  [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize)]
  public async Task<ActionResult<ImportResultDto>> Import(
    string config,
    IFormFile file,
    [FromForm] ImportRecordsOptions options,
    CancellationToken cancellationToken)
  {
    if (file.Length == 0)
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Некоректні дані", detail: "Файл порожній");

    await using var stream = file.OpenReadStream();
    return await dataImportService.Import(config, stream, file.FileName, options, cancellationToken);
  }
}
