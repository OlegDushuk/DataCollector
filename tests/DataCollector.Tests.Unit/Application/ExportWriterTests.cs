using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DataCollector.Server.Application.Export;

namespace DataCollector.Tests.Unit.Application;

/// <summary>Файли експорту: коректні формати, екранування і захист від formula injection.</summary>
public class ExportWriterTests
{
  private static ExportTable Table(params object?[][] rows) => new()
  {
    Title = "Замовлення",
    Model = "orders",
    Kind = "records",
    ExportedAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero),
    Columns =
    [
      new ExportColumn("id", "Id", ExportValueKind.Text, IsTechnical: true),
      new ExportColumn("number", "Номер", ExportValueKind.Text),
      new ExportColumn("price", "Сума", ExportValueKind.Number),
      new ExportColumn("paid", "Оплачено", ExportValueKind.Boolean),
      new ExportColumn("createdAt", "Створено", ExportValueKind.DateTime)
    ],
    Rows = rows.ToAsyncEnumerable(),
    RowCount = rows.Length
  };

  private static async Task<byte[]> Write(ExportFormat format, ExportTable table)
  {
    using var output = new MemoryStream();
    await format.Writer().WriteAsync(table, output, CancellationToken.None);
    return output.ToArray();
  }

  private static readonly DateTime Created = new(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc);

  [Fact]
  public async Task Csv_Rfc4180_WithBom_AndQuotedValues()
  {
    var bytes = await Write(ExportFormat.Csv, Table(
      [Guid.Empty, "ORD-1, термінове", 120.5m, true, Created],
      [Guid.Empty, "Сказав \"так\"", 7m, false, Created]));

    Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    Assert.Equal("Номер,Сума,Оплачено,Створено", lines[0]);   // технічна колонка Id у CSV не потрапляє
    Assert.StartsWith("\"ORD-1, термінове\",120.5,", lines[1]);
    Assert.StartsWith("\"Сказав \"\"так\"\"\",7,", lines[2]);
  }

  [Theory]
  [InlineData("=HYPERLINK(\"http://evil\")")]
  [InlineData("+380501234567")]
  [InlineData("-1+1")]
  [InlineData("@SUM(A1)")]
  public async Task Csv_FormulaInjection_IsNeutralized(string text)
  {
    var bytes = await Write(ExportFormat.Csv, Table([Guid.Empty, text, -5m, true, Created]));
    var row = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n")[1];

    Assert.Contains("'" + text[0], row);
    Assert.Contains(",-5,", row);   // справжні від'ємні числа не змінюються
  }

  [Fact]
  public async Task Xlsx_IsValidZip_WithSheetAndSharedHeader()
  {
    var bytes = await Write(ExportFormat.Xlsx, Table([Guid.Empty, "ORD-1", 120.5m, true, Created]));

    using var zip = new ZipArchive(new MemoryStream(bytes));
    Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
    var sheetEntry = zip.Entries.First(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal));

    var sheet = XDocument.Load(sheetEntry.Open());
    var text = sheet.ToString();
    Assert.Contains("ORD-1", text);
    Assert.Contains("120.5", text);
  }

  [Fact]
  public async Task Json_HasColumnsRowsAndTypes()
  {
    var bytes = await Write(ExportFormat.Json, Table([Guid.NewGuid(), "ORD-1", 120.5m, true, Created]));

    using var json = JsonDocument.Parse(bytes);
    var text = json.RootElement.ToString();
    Assert.Contains("ORD-1", text);
    Assert.Contains("120.5", text);
    Assert.Contains("number", text);
  }

  [Fact]
  public async Task Xml_IsWellFormed()
  {
    var bytes = await Write(ExportFormat.Xml, Table([Guid.NewGuid(), "<script>&", 1m, false, Created]));

    var document = XDocument.Load(new MemoryStream(bytes));
    Assert.Contains("<script>&", document.Root!.Value);
  }

  [Theory]
  [InlineData("csv", ExportFormat.Csv)]
  [InlineData("XLSX", ExportFormat.Xlsx)]
  public void Format_ParsedCaseInsensitive(string code, ExportFormat expected)
  {
    Assert.Equal(expected, ExportFormats.Parse(code));
  }

  [Fact]
  public void Format_Unknown_Rejected()
  {
    Assert.Throws<DataCollector.Server.Domain.Exceptions.DomainValidationException>(() => ExportFormats.Parse("pdf"));
  }
}
