using System.Net.Mime;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using DataCollector.Server.Application.ExternalContracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace DataCollector.Server.API.Controllers;

/// <summary>
/// Вивантаження записів і зведень у файли: CSV, Excel (xlsx), JSON, XML.
/// Умови, пошук, сортування і групування - ті самі, що в records/query і records/aggregate.
/// </summary>
[ApiController]
[Route("api/data-configs/{config}/records")]
public class DataExportController(IDataExportService dataExportService) : ControllerBase
{
  /// <summary>Заголовок відповіді: "true", якщо в файл потрапили не всі записи (ліміт експорту).</summary>
  public const string TruncatedHeader = "X-Export-Truncated";

  public const string RowCountHeader = "X-Export-Rows";

  [HttpPost("export")]
  public Task Export(string config, [FromBody] ExportRecordsCommand command, CancellationToken cancellationToken)
    => SendFile(output => dataExportService.ExportRecords(config, command, output, cancellationToken), cancellationToken);

  [HttpPost("aggregate/export")]
  public Task ExportAggregate(string config, [FromBody] ExportAggregateCommand command, CancellationToken cancellationToken)
    => SendFile(output => dataExportService.ExportAggregate(config, command, output, cancellationToken), cancellationToken);

  /// <summary>
  /// Файл спершу пишеться в буфер (пам'ять, а великий - тимчасовий файл на диску), і лише потім віддається.
  /// Так помилка під час формування (невірна умова, обірване з'єднання з БД) стає звичайною відповіддю
  /// з помилкою, а не обрізаним файлом, а клієнт заздалегідь знає розмір файлу.
  /// </summary>
  private async Task SendFile(Func<Stream, Task<ExportFileInfo>> write, CancellationToken cancellationToken)
  {
    await using var buffer = new FileBufferingWriteStream();
    var file = await write(buffer);

    Response.ContentType = file.ContentType;
    Response.ContentLength = buffer.Length;
    Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue(DispositionTypeNames.Attachment)
    {
      FileName = file.FileName,
      FileNameStar = file.FileName
    }.ToString();
    Response.Headers[RowCountHeader] = file.RowCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    Response.Headers[TruncatedHeader] = file.Truncated ? "true" : "false";

    await buffer.DrainBufferAsync(Response.Body, cancellationToken);
  }
}
