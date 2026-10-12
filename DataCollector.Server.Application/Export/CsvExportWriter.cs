using System.Text;

namespace DataCollector.Server.Application.Export;

/// <summary>
/// CSV за RFC 4180: кома, рядки CRLF, лапки навколо значень з комою, лапками або переносом.
/// UTF-8 з BOM, щоб Excel правильно відкрив кирилицю.
/// </summary>
/// <remarks>
/// Захист від CSV-ін'єкції (formula injection): текст, що починається з =, +, -, @, табуляції чи CR,
/// Excel виконав би як формулу, тому перед ним ставиться апостроф. Числа й дати не змінюються.
/// </remarks>
internal sealed class CsvExportWriter : IExportWriter
{
  private const char Separator = ',';

  public async Task WriteAsync(ExportTable table, Stream output, CancellationToken cancellationToken)
  {
    var columns = table.Columns.Where(c => !c.IsTechnical).ToList();
    var indexes = columns.Select(c => IndexOf(table.Columns, c)).ToList();

    await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 64 * 1024, leaveOpen: true);
    writer.NewLine = "\r\n";

    await writer.WriteLineAsync(string.Join(Separator, columns.Select(c => Escape(c.Title))));

    var line = new StringBuilder();
    await foreach (var row in table.Rows.WithCancellation(cancellationToken))
    {
      line.Clear();
      for (var i = 0; i < indexes.Count; i++)
      {
        if (i > 0)
          line.Append(Separator);

        line.Append(Cell(row[indexes[i]]));
      }

      await writer.WriteLineAsync(line, cancellationToken);
    }

    await writer.FlushAsync(cancellationToken);
  }

  private static string Cell(object? value)
  {
    var text = ExportValues.Text(value);
    if (text is null)
      return string.Empty;

    if (value is string && text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
      text = "'" + text;

    return Escape(text);
  }

  private static string Escape(string text)
  {
    if (text.IndexOfAny([Separator, '"', '\r', '\n']) < 0)
      return text;

    return $"\"{text.Replace("\"", "\"\"")}\"";
  }

  private static int IndexOf(IReadOnlyList<ExportColumn> columns, ExportColumn column)
  {
    for (var i = 0; i < columns.Count; i++)
    {
      if (ReferenceEquals(columns[i], column))
        return i;
    }

    return -1;
  }
}
