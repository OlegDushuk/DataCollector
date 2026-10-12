using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace DataCollector.Server.Application.Export;

/// <summary>
/// Excel-файл (Office Open XML, SpreadsheetML) без сторонніх бібліотек: ZIP з кількох XML-частин.
/// Аркуш пишеться потоково (рядок за рядком), тож великий експорт не тримає всю книгу в пам'яті.
/// </summary>
/// <remarks>
/// Комірки типізовані: числа - числами, так/ні - логічними, дати - числами Excel зі стилем дати
/// (сортуються й фільтруються як дати), текст - inline-рядками (без таблиці спільних рядків,
/// яку довелося б збирати до кінця). Заголовок жирний і закріплений, на ньому автофільтр.
/// </remarks>
internal sealed class XlsxExportWriter : IExportWriter
{
  /// <summary>Excel обмежує текст у комірці 32 767 символами.</summary>
  private const int MaxCellText = 32_767;

  // Індекси стилів у styles.xml (cellXfs).
  private const int StyleHeader = 1;
  private const int StyleDate = 2;
  private const int StyleDateTime = 3;

  private static readonly DateTime ExcelEpoch = new(1899, 12, 30);

  public async Task WriteAsync(ExportTable table, Stream output, CancellationToken cancellationToken)
  {
    var columns = table.Columns.Where(c => !c.IsTechnical).ToList();
    var indexes = columns.Select(c => table.Columns.ToList().IndexOf(c)).ToList();

    using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
    {
      await WritePart(zip, "[Content_Types].xml", ContentTypes);
      await WritePart(zip, "_rels/.rels", RootRelationships);
      await WritePart(zip, "xl/workbook.xml", Workbook(SheetName(table.Title)));
      await WritePart(zip, "xl/_rels/workbook.xml.rels", WorkbookRelationships);
      await WritePart(zip, "xl/styles.xml", Styles);

      var entry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
      await using var stream = entry.Open();
      await WriteSheet(stream, table, columns, indexes, cancellationToken);
    }

    await output.FlushAsync(cancellationToken);
  }

  private static async Task WriteSheet(
    Stream stream,
    ExportTable table,
    IReadOnlyList<ExportColumn> columns,
    IReadOnlyList<int> indexes,
    CancellationToken cancellationToken)
  {
    var settings = new XmlWriterSettings { Async = true, Encoding = new UTF8Encoding(false) };
    await using var xml = XmlWriter.Create(stream, settings);

    const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    var lastColumn = ColumnName(Math.Max(columns.Count, 1) - 1);

    await xml.WriteStartDocumentAsync(true);
    await xml.WriteStartElementAsync(null, "worksheet", ns);

    // Закріплений рядок заголовка.
    await xml.WriteStartElementAsync(null, "sheetViews", ns);
    await xml.WriteStartElementAsync(null, "sheetView", ns);
    await xml.WriteAttributeStringAsync(null, "workbookViewId", null, "0");
    await xml.WriteStartElementAsync(null, "pane", ns);
    await xml.WriteAttributeStringAsync(null, "ySplit", null, "1");
    await xml.WriteAttributeStringAsync(null, "topLeftCell", null, "A2");
    await xml.WriteAttributeStringAsync(null, "activePane", null, "bottomLeft");
    await xml.WriteAttributeStringAsync(null, "state", null, "frozen");
    await xml.WriteEndElementAsync();
    await xml.WriteEndElementAsync();
    await xml.WriteEndElementAsync();

    // Ширина колонок - за заголовком і типом (значення ще не прочитані: аркуш пишеться потоково).
    if (columns.Count > 0)
    {
      await xml.WriteStartElementAsync(null, "cols", ns);
      for (var i = 0; i < columns.Count; i++)
      {
        await xml.WriteStartElementAsync(null, "col", ns);
        await xml.WriteAttributeStringAsync(null, "min", null, (i + 1).ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "max", null, (i + 1).ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "width", null, Width(columns[i]).ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "customWidth", null, "1");
        await xml.WriteEndElementAsync();
      }
      await xml.WriteEndElementAsync();
    }

    await xml.WriteStartElementAsync(null, "sheetData", ns);

    var rowNumber = 1;
    await xml.WriteStartElementAsync(null, "row", ns);
    await xml.WriteAttributeStringAsync(null, "r", null, "1");
    for (var i = 0; i < columns.Count; i++)
      await WriteText(xml, ns, i, rowNumber, columns[i].Title, StyleHeader);
    await xml.WriteEndElementAsync();

    await foreach (var row in table.Rows.WithCancellation(cancellationToken))
    {
      rowNumber++;
      await xml.WriteStartElementAsync(null, "row", ns);
      await xml.WriteAttributeStringAsync(null, "r", null, rowNumber.ToString(CultureInfo.InvariantCulture));

      for (var i = 0; i < indexes.Count; i++)
        await WriteCell(xml, ns, i, rowNumber, row[indexes[i]]);

      await xml.WriteEndElementAsync();
    }

    await xml.WriteEndElementAsync(); // sheetData

    if (columns.Count > 0)
    {
      await xml.WriteStartElementAsync(null, "autoFilter", ns);
      await xml.WriteAttributeStringAsync(null, "ref", null, $"A1:{lastColumn}{rowNumber}");
      await xml.WriteEndElementAsync();
    }

    await xml.WriteEndElementAsync(); // worksheet
    await xml.WriteEndDocumentAsync();
    await xml.FlushAsync();
  }

  private static Task WriteCell(XmlWriter xml, string ns, int column, int row, object? value) => value switch
  {
    null => Task.CompletedTask,
    // Числа - загальним форматом Excel: показує стільки знаків, скільки є (5, 12.75).
    decimal number => WriteNumber(xml, ns, column, row, number.ToString(CultureInfo.InvariantCulture), 0),
    long number => WriteNumber(xml, ns, column, row, number.ToString(CultureInfo.InvariantCulture), 0),
    bool flag => WriteBoolean(xml, ns, column, row, flag),
    DateOnly date => WriteNumber(xml, ns, column, row, Serial(date.ToDateTime(TimeOnly.MinValue)), StyleDate),
    // Дата з часом уже в потрібному часовому поясі - Excel поясів не знає, пишемо місцевий час.
    DateTimeOffset dateTime => WriteNumber(xml, ns, column, row, Serial(dateTime.DateTime), StyleDateTime),
    _ => WriteText(xml, ns, column, row, ExportValues.Text(value) ?? string.Empty, 0)
  };

  private static async Task WriteNumber(XmlWriter xml, string ns, int column, int row, string value, int style)
  {
    await StartCell(xml, ns, column, row, style, type: null);
    await xml.WriteElementStringAsync(null, "v", ns, value);
    await xml.WriteEndElementAsync();
  }

  private static async Task WriteBoolean(XmlWriter xml, string ns, int column, int row, bool value)
  {
    await StartCell(xml, ns, column, row, 0, "b");
    await xml.WriteElementStringAsync(null, "v", ns, value ? "1" : "0");
    await xml.WriteEndElementAsync();
  }

  private static async Task WriteText(XmlWriter xml, string ns, int column, int row, string text, int style)
  {
    if (text.Length > MaxCellText)
      text = text[..MaxCellText];

    await StartCell(xml, ns, column, row, style, "inlineStr");
    await xml.WriteStartElementAsync(null, "is", ns);
    await xml.WriteStartElementAsync(null, "t", ns);

    // Пробіли на краях Excel відкидає без xml:space="preserve".
    if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1])))
      await xml.WriteAttributeStringAsync("xml", "space", null, "preserve");

    await xml.WriteStringAsync(ExportValues.XmlSafe(text));
    await xml.WriteEndElementAsync();
    await xml.WriteEndElementAsync();
    await xml.WriteEndElementAsync();
  }

  private static async Task StartCell(XmlWriter xml, string ns, int column, int row, int style, string? type)
  {
    await xml.WriteStartElementAsync(null, "c", ns);
    await xml.WriteAttributeStringAsync(null, "r", null, ColumnName(column) + row.ToString(CultureInfo.InvariantCulture));

    if (style != 0)
      await xml.WriteAttributeStringAsync(null, "s", null, style.ToString(CultureInfo.InvariantCulture));

    if (type != null)
      await xml.WriteAttributeStringAsync(null, "t", null, type);
  }

  /// <summary>
  /// Дата як число Excel: дні від 30.12.1899 з дробовою частиною для часу. Повна точність double ("R"):
  /// при округленні 00:30 перетворювалось на 00:29:59.99, і формат "hh:mm" показував 00:29.
  /// </summary>
  private static string Serial(DateTime value)
  {
    var days = (value.Date - ExcelEpoch).Days;
    var fraction = value.TimeOfDay.Ticks / (double)TimeSpan.TicksPerDay;
    return (days + fraction).ToString("R", CultureInfo.InvariantCulture);
  }

  /// <summary>Номер колонки (з 0) -> буквене ім'я: 0 -> A, 25 -> Z, 26 -> AA.</summary>
  internal static string ColumnName(int index)
  {
    var name = string.Empty;
    for (index++; index > 0; index = (index - 1) / 26)
      name = (char)('A' + (index - 1) % 26) + name;

    return name;
  }

  private static int Width(ExportColumn column)
  {
    var byType = column.Kind switch
    {
      ExportValueKind.DateTime => 18,
      ExportValueKind.Date => 12,
      ExportValueKind.Text => 20,
      _ => 12
    };

    return Math.Clamp(Math.Max(byType, column.Title.Length + 4), 8, 60);
  }

  /// <summary>Назва аркуша: до 31 символу, без символів, заборонених Excel.</summary>
  private static string SheetName(string title)
  {
    var name = new string(title.Where(c => c is not ('\\' or '/' or '?' or '*' or '[' or ']' or ':')).ToArray()).Trim('\'').Trim();
    if (name.Length > 31)
      name = name[..31];

    return name.Length == 0 ? "Дані" : name;
  }

  private static async Task WritePart(ZipArchive zip, string path, string content)
  {
    var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
    await using var stream = entry.Open();
    var bytes = Encoding.UTF8.GetBytes(content);
    await stream.WriteAsync(bytes);
  }

  private static string Workbook(string sheetName) => $"""
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
      <sheets><sheet name="{System.Security.SecurityElement.Escape(ExportValues.XmlSafe(sheetName))}" sheetId="1" r:id="rId1"/></sheets>
    </workbook>
    """;

  private const string ContentTypes = """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
      <Default Extension="xml" ContentType="application/xml"/>
      <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
      <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
      <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
    </Types>
    """;

  private const string RootRelationships = """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
    </Relationships>
    """;

  private const string WorkbookRelationships = """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
      <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
    </Relationships>
    """;

  /// <summary>Стилі: 0 - звичайний, 1 - заголовок (жирний, фон), 2 - дата, 3 - дата й час.</summary>
  private const string Styles = """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
      <numFmts count="2">
        <numFmt numFmtId="164" formatCode="dd.mm.yyyy"/>
        <numFmt numFmtId="165" formatCode="dd.mm.yyyy hh:mm"/>
      </numFmts>
      <fonts count="2">
        <font><sz val="11"/><name val="Calibri"/><family val="2"/></font>
        <font><b/><sz val="11"/><name val="Calibri"/><family val="2"/></font>
      </fonts>
      <fills count="3">
        <fill><patternFill patternType="none"/></fill>
        <fill><patternFill patternType="gray125"/></fill>
        <fill><patternFill patternType="solid"><fgColor rgb="FFEEEDFB"/><bgColor indexed="64"/></patternFill></fill>
      </fills>
      <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
      <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
      <cellXfs count="4">
        <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
        <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
        <xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
        <xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
      </cellXfs>
      <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
    </styleSheet>
    """;
}
