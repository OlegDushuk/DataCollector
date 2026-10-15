using System.Text;
using DataCollector.Server.Application.ExternalContracts.Models;

namespace DataCollector.Tests.Unit.Application;

/// <summary>Імпорт записів з CSV: зіставлення колонок, перевірка значень, пробний запуск, оновлення наявних.</summary>
public class ImportTests
{
  private readonly ServiceFactory _services = new();

  public ImportTests() => _services.AddModel(TestModels.Orders());

  private Task<ImportResultDto> Import(string csv, ImportRecordsOptions options)
    => _services.Import().Import("orders", new MemoryStream(Encoding.UTF8.GetBytes(csv)), "orders.csv", options);

  [Fact]
  public async Task Csv_ByKeysAndNames_CreatesRecords()
  {
    var result = await Import(
      "number;Сума;paid;status\nORD-1;120,5;так;sent\nORD-2;99;0;done\n",
      new ImportRecordsOptions());

    Assert.True(result.Applied);
    Assert.Equal(2, result.Created);
    Assert.Equal(0, result.Invalid);
    Assert.Equal(2, _services.Store.Instances.Count);
    Assert.Contains(_services.Store.Instances.Values, i => i.Values.ContainsValue(120.5m));
  }

  [Fact]
  public async Task DryRun_ReportsWithoutWriting()
  {
    var result = await Import("number,price\nORD-1,10\n", new ImportRecordsOptions { DryRun = true });

    Assert.False(result.Applied);
    Assert.Equal(1, result.Created);
    Assert.Empty(_services.Store.Instances);
  }

  [Fact]
  public async Task InvalidRow_WithoutSkip_NothingIsWritten()
  {
    var result = await Import("number,price\nORD-1,10\nORD-2,дорого\n,5\n", new ImportRecordsOptions());

    Assert.False(result.Applied);
    Assert.Equal(2, result.Invalid);
    Assert.Contains(result.Errors, e => e.Row == 3 && e.Message.Contains("не є числом"));
    Assert.Empty(_services.Store.Instances);
  }

  [Fact]
  public async Task InvalidRow_WithSkip_ValidRowsWritten()
  {
    var result = await Import("number,price\nORD-1,10\nORD-2,-1\n", new ImportRecordsOptions { SkipInvalid = true });

    Assert.True(result.Applied);
    Assert.Equal(1, result.Created);
    Assert.Equal(1, result.Invalid);
  }

  [Fact]
  public async Task ExistingId_UpdatesOnlyImportedColumns()
  {
    await Import("number,price,status\nORD-1,10,sent\n", new ImportRecordsOptions());
    var id = _services.Store.Instances.Keys.Single();

    var result = await Import($"id,price\n{id},15\n", new ImportRecordsOptions { UpdateExisting = true });

    Assert.Equal(1, result.Updated);
    var values = _services.Store.Instances[id].Values.Values.ToList();
    Assert.Contains(15m, values);
    Assert.Contains("sent", values);   // колонки, якої немає у файлі, імпорт не чіпає
  }

  [Fact]
  public async Task UnknownColumn_IsReportedAndIgnored()
  {
    var result = await Import("number,discount\nORD-1,5\n", new ImportRecordsOptions());

    Assert.Equal(1, result.Created);
    Assert.Contains(result.Columns, c => c.Header == "discount" && c.FieldKey is null);
  }
}
