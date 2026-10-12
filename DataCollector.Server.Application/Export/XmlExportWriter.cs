using System.Text;
using System.Xml;

namespace DataCollector.Server.Application.Export;

/// <summary>
/// XML: &lt;export model title kind exportedAt&gt; з описом колонок (&lt;columns&gt;), рядками (&lt;rows&gt;),
/// де кожне значення - елемент з іменем ключа поля (порожнє значення - елемент відсутній),
/// і підсумком &lt;summary rowCount truncated&gt;.
/// </summary>
/// <remarks>
/// Ключі полів - латинські ідентифікатори, але ім'я все одно кодується XmlConvert.EncodeLocalName,
/// щоб будь-який ключ давав коректний XML.
/// </remarks>
internal sealed class XmlExportWriter : IExportWriter
{
  public async Task WriteAsync(ExportTable table, Stream output, CancellationToken cancellationToken)
  {
    var settings = new XmlWriterSettings
    {
      Async = true,
      Indent = true,
      Encoding = new UTF8Encoding(false)
    };

    var names = table.Columns.Select(c => XmlConvert.EncodeLocalName(c.Key)).ToList();

    await using var xml = XmlWriter.Create(output, settings);

    await xml.WriteStartDocumentAsync();
    await xml.WriteStartElementAsync(null, "export", null);
    await xml.WriteAttributeStringAsync(null, "model", null, table.Model);
    await xml.WriteAttributeStringAsync(null, "title", null, ExportValues.XmlSafe(table.Title));
    await xml.WriteAttributeStringAsync(null, "kind", null, table.Kind);
    await xml.WriteAttributeStringAsync(null, "exportedAt", null, ExportValues.Text(table.ExportedAt));

    await xml.WriteStartElementAsync(null, "columns", null);
    for (var i = 0; i < table.Columns.Count; i++)
    {
      var column = table.Columns[i];
      await xml.WriteStartElementAsync(null, "column", null);
      await xml.WriteAttributeStringAsync(null, "key", null, names[i]);
      await xml.WriteAttributeStringAsync(null, "title", null, ExportValues.XmlSafe(column.Title));
      await xml.WriteAttributeStringAsync(null, "type", null, char.ToLowerInvariant(column.Kind.ToString()[0]) + column.Kind.ToString()[1..]);
      await xml.WriteEndElementAsync();
    }
    await xml.WriteEndElementAsync();

    await xml.WriteStartElementAsync(null, "rows", null);
    await foreach (var row in table.Rows.WithCancellation(cancellationToken))
    {
      await xml.WriteStartElementAsync(null, "row", null);
      for (var i = 0; i < table.Columns.Count; i++)
      {
        if (ExportValues.Text(row[i]) is not { } text)
          continue;

        await xml.WriteElementStringAsync(null, names[i], null, ExportValues.XmlSafe(text));
      }
      await xml.WriteEndElementAsync();
    }
    await xml.WriteEndElementAsync();

    // Кількість і обрізання відомі лише після рядків, тож - підсумковим елементом.
    await xml.WriteStartElementAsync(null, "summary", null);
    await xml.WriteAttributeStringAsync(null, "rowCount", null, table.RowCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
    await xml.WriteAttributeStringAsync(null, "truncated", null, table.Truncated ? "true" : "false");
    await xml.WriteEndElementAsync();

    await xml.WriteEndElementAsync();
    await xml.WriteEndDocumentAsync();
    await xml.FlushAsync();
  }
}
