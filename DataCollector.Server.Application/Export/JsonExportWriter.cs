using System.Text.Encodings.Web;
using System.Text.Json;

namespace DataCollector.Server.Application.Export;

/// <summary>
/// JSON: { "model", "title", "kind", "exportedAt", "columns": [{ key, title, type }], "rows": [{ ключ: значення }],
/// "rowCount", "truncated" }. Числа - числами, true/false - логічними, дати - рядками ISO 8601.
/// </summary>
internal sealed class JsonExportWriter : IExportWriter
{
  private static readonly JsonWriterOptions Options = new()
  {
    Indented = true,
    // Кирилиця як є, а не И...: файл читає і людина.
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
  };

  public async Task WriteAsync(ExportTable table, Stream output, CancellationToken cancellationToken)
  {
    await using var json = new Utf8JsonWriter(output, Options);

    json.WriteStartObject();
    json.WriteString("model", table.Model);
    json.WriteString("title", table.Title);
    json.WriteString("kind", table.Kind);
    json.WriteString("exportedAt", ExportValues.Text(table.ExportedAt));

    json.WriteStartArray("columns");
    foreach (var column in table.Columns)
    {
      json.WriteStartObject();
      json.WriteString("key", column.Key);
      json.WriteString("title", column.Title);
      json.WriteString("type", JsonNamingPolicy.CamelCase.ConvertName(column.Kind.ToString()));
      json.WriteEndObject();
    }
    json.WriteEndArray();

    json.WriteStartArray("rows");
    await foreach (var row in table.Rows.WithCancellation(cancellationToken))
    {
      json.WriteStartObject();
      for (var i = 0; i < table.Columns.Count; i++)
      {
        json.WritePropertyName(table.Columns[i].Key);
        WriteValue(json, row[i]);
      }
      json.WriteEndObject();

      // Скидаємо буфер частинами, щоб не тримати весь файл у пам'яті.
      if (json.BytesPending > 64 * 1024)
        await json.FlushAsync(cancellationToken);
    }
    json.WriteEndArray();

    json.WriteNumber("rowCount", table.RowCount);
    json.WriteBoolean("truncated", table.Truncated);
    json.WriteEndObject();

    await json.FlushAsync(cancellationToken);
  }

  private static void WriteValue(Utf8JsonWriter json, object? value)
  {
    switch (value)
    {
      case null:
        json.WriteNullValue();
        break;
      case decimal number:
        json.WriteNumberValue(number);
        break;
      case long number:
        json.WriteNumberValue(number);
        break;
      case bool flag:
        json.WriteBooleanValue(flag);
        break;
      default:
        json.WriteStringValue(ExportValues.Text(value));
        break;
    }
  }
}
