using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Application.Import;

/// <summary>
/// Перший аркуш книги Excel (.xlsx, Office Open XML) без сторонніх бібліотек: ZIP з XML-частинами.
/// Перший непорожній рядок - заголовки, далі - дані.
/// </summary>
/// <remarks>
/// Excel зберігає дату як число днів від 30.12.1899, а що це дата - видно лише зі стилю комірки
/// (формат числа). Тому читаються і стилі: числа з форматом дати повертаються як 2026-10-11
/// або 2026-10-11T14:30:00. Текст - зі спільних рядків (sharedStrings) або вбудований (inlineStr).
/// </remarks>
internal sealed partial class XlsxImportReader : IImportReader
{
  /// <summary>Захист від «ZIP-бомби»: найбільший розмір однієї розпакованої частини.</summary>
  private const long MaxPartSize = 512L * 1024 * 1024;

  private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
  private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
  private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

  /// <summary>Вбудовані формати дат Excel (numFmtId).</summary>
  private static readonly HashSet<int> BuiltInDateFormats = [14, 15, 16, 17, 22, 27, 30, 36, 45, 46, 47, 50, 57];
  private static readonly HashSet<int> BuiltInDateTimeFormats = [18, 19, 20, 21, 22, 45, 46, 47];

  [GeneratedRegex("\"[^\"]*\"|\\[[^\\]]*\\]|\\\\.")]
  private static partial Regex FormatLiterals();

  public ImportTable Read(Stream input, int maxRows)
  {
    ZipArchive zip;
    try
    {
      zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
    }
    catch (InvalidDataException)
    {
      throw new DomainValidationException("Файл не є книгою Excel (.xlsx) або пошкоджений");
    }

    using (zip)
    {
      var sheetPath = FirstSheetPath(zip);
      var sharedStrings = ReadSharedStrings(zip);
      var dateStyles = ReadDateStyles(zip);

      return ReadSheet(zip, sheetPath, sharedStrings, dateStyles, maxRows);
    }
  }

  private static string FirstSheetPath(ZipArchive zip)
  {
    var workbook = Load(zip, "xl/workbook.xml") ?? throw new DomainValidationException("У книзі Excel немає аркушів");
    var ns = Namespaces(workbook);

    var sheet = workbook.SelectSingleNode("//m:sheets/m:sheet", ns) as XmlElement
                ?? throw new DomainValidationException("У книзі Excel немає аркушів");
    var relationId = sheet.GetAttribute("id", RelNs);

    var rels = Load(zip, "xl/_rels/workbook.xml.rels");
    if (rels != null)
    {
      foreach (XmlElement rel in rels.GetElementsByTagName("Relationship", PackageRelNs))
      {
        if (rel.GetAttribute("Id") == relationId)
          return ResolvePath("xl/", rel.GetAttribute("Target"));
      }
    }

    return "xl/worksheets/sheet1.xml";
  }

  private static List<string> ReadSharedStrings(ZipArchive zip)
  {
    var result = new List<string>();
    var entry = zip.GetEntry("xl/sharedStrings.xml");
    if (entry is null)
      return result;

    using var reader = XmlReader.Create(OpenLimited(entry), Settings);
    StringBuilder? text = null;
    var phonetic = 0;
    var advanced = false;

    // ReadElementContentAsString сам переходить на наступний вузол - тоді його не можна пропустити черговим Read().
    while (advanced || reader.Read())
    {
      advanced = false;

      if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si")
      {
        if (reader.IsEmptyElement)
          result.Add(string.Empty);
        else
          text = new StringBuilder();
      }
      else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "rPh" && !reader.IsEmptyElement)
        phonetic++;
      else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "rPh")
        phonetic--;
      else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && text != null && phonetic == 0)
      {
        text.Append(reader.ReadElementContentAsString());
        advanced = true;
      }
      else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "si" && text != null)
      {
        result.Add(text.ToString());
        text = null;
      }
    }

    return result;
  }

  /// <summary>Індекс стилю комірки (cellXfs) -> чи це дата (false) або дата з часом (true).</summary>
  private static Dictionary<int, bool> ReadDateStyles(ZipArchive zip)
  {
    var result = new Dictionary<int, bool>();
    var styles = Load(zip, "xl/styles.xml");
    if (styles is null)
      return result;

    var ns = Namespaces(styles);
    var customFormats = new Dictionary<int, string>();
    foreach (XmlElement format in styles.SelectNodes("//m:numFmts/m:numFmt", ns)!)
    {
      if (int.TryParse(format.GetAttribute("numFmtId"), out var id))
        customFormats[id] = format.GetAttribute("formatCode");
    }

    var index = 0;
    foreach (XmlElement xf in styles.SelectNodes("//m:cellXfs/m:xf", ns)!)
    {
      if (int.TryParse(xf.GetAttribute("numFmtId"), out var formatId))
      {
        if (customFormats.TryGetValue(formatId, out var code))
        {
          var kind = DateKind(code);
          if (kind != null)
            result[index] = kind.Value;
        }
        else if (BuiltInDateFormats.Contains(formatId) || BuiltInDateTimeFormats.Contains(formatId))
        {
          result[index] = BuiltInDateTimeFormats.Contains(formatId);
        }
      }

      index++;
    }

    return result;
  }

  /// <summary>Чи є формат числа датою: є d/y (або m разом із ними), h/s - з часом. null - не дата.</summary>
  internal static bool? DateKind(string formatCode)
  {
    // Текст у лапках, [Red]/[$-uk-UA] і екрановані символи не є частиною формату дати.
    var code = FormatLiterals().Replace(formatCode, string.Empty).ToLowerInvariant();
    var section = code.Split(';')[0];

    var hasDate = section.Contains('d') || section.Contains('y');
    var hasTime = section.Contains('h') || section.Contains('s');

    if (!hasDate && !hasTime)
      return null;

    // "0.00" або "#,##0" - не дати; "mm:ss" без дати - тривалість, її краще прочитати числом.
    return hasDate ? hasTime : null;
  }

  private static ImportTable ReadSheet(
    ZipArchive zip, string path, List<string> sharedStrings, Dictionary<int, bool> dateStyles, int maxRows)
  {
    var entry = zip.GetEntry(path) ?? throw new DomainValidationException("Аркуш книги Excel не знайдено");

    List<string>? headers = null;
    var rows = new List<ImportRow>();
    var cells = new SortedDictionary<int, string?>();
    var rowNumber = 0;
    var nextColumn = 0;

    using var reader = XmlReader.Create(OpenLimited(entry), Settings);

    while (reader.Read())
    {
      if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
      {
        rowNumber = int.TryParse(reader.GetAttribute("r"), out var r) ? r : rowNumber + 1;
        cells.Clear();
        nextColumn = 0;

        if (reader.IsEmptyElement)
          continue;
      }
      else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "c")
      {
        var column = ColumnIndex(reader.GetAttribute("r")) ?? nextColumn;
        nextColumn = column + 1;

        var type = reader.GetAttribute("t");
        var style = int.TryParse(reader.GetAttribute("s"), out var s) ? s : 0;
        var value = reader.IsEmptyElement ? null : ReadCell(reader, type, style, sharedStrings, dateStyles);

        if (column < 16_384)
          cells[column] = value;
      }
      else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "row")
      {
        if (!cells.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
          continue;

        if (headers is null)
        {
          var width = cells.Keys.Max() + 1;
          headers = Enumerable.Range(0, width).Select(i => cells.GetValueOrDefault(i)?.Trim() ?? string.Empty).ToList();
          continue;
        }

        if (rows.Count == maxRows)
          throw ImportFormats.TooManyRows(maxRows);

        rows.Add(new ImportRow(rowNumber,
          Enumerable.Range(0, headers.Count).Select(i => cells.GetValueOrDefault(i)).ToArray()));
      }
    }

    return headers is null
      ? throw new DomainValidationException("Аркуш порожній: немає рядка заголовків")
      : new ImportTable { Headers = headers, Rows = rows };
  }

  /// <summary>Значення комірки як текст. Читач стоїть на елементі &lt;c&gt;.</summary>
  private static string? ReadCell(
    XmlReader reader, string? type, int style, List<string> sharedStrings, Dictionary<int, bool> dateStyles)
  {
    string? raw = null;
    StringBuilder? inline = null;
    var depth = reader.Depth;
    var advanced = false;

    while (advanced || reader.Read())
    {
      advanced = false;

      if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
        break;

      if (reader.NodeType != XmlNodeType.Element)
        continue;

      if (reader.LocalName == "v")
      {
        raw = reader.ReadElementContentAsString();
        advanced = true;
      }
      else if (reader.LocalName == "t" && type == "inlineStr")
      {
        (inline ??= new StringBuilder()).Append(reader.ReadElementContentAsString());
        advanced = true;
      }
    }

    switch (type)
    {
      case "inlineStr":
        return inline?.ToString();
      case "s":
        return int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : null;
      case "b":
        return raw == "1" ? "true" : raw == "0" ? "false" : raw;
      case "str" or "e" or "d":
        return raw;
    }

    if (raw is null)
      return null;

    if (dateStyles.TryGetValue(style, out var withTime) &&
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) &&
        serial is > -657435 and < 2958466)
    {
      var date = DateTime.FromOADate(serial);
      // Округлення до секунди: Excel зберігає час як частку доби з похибкою подвійної точності.
      date = new DateTime(date.Ticks - date.Ticks % TimeSpan.TicksPerSecond +
                          (date.Ticks % TimeSpan.TicksPerSecond >= TimeSpan.TicksPerSecond / 2 ? TimeSpan.TicksPerSecond : 0));

      return withTime || date.TimeOfDay != TimeSpan.Zero
        ? date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
        : date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    return raw;
  }

  /// <summary>"AB12" -> 27 (з нуля).</summary>
  internal static int? ColumnIndex(string? reference)
  {
    if (string.IsNullOrEmpty(reference))
      return null;

    var index = 0;
    var letters = 0;
    foreach (var c in reference)
    {
      if (c is >= 'A' and <= 'Z')
      {
        index = index * 26 + (c - 'A' + 1);
        letters++;
      }
      else if (c is >= 'a' and <= 'z')
      {
        index = index * 26 + (c - 'a' + 1);
        letters++;
      }
      else
        break;
    }

    return letters is > 0 and <= 3 ? index - 1 : null;
  }

  private static readonly XmlReaderSettings Settings = new()
  {
    DtdProcessing = DtdProcessing.Prohibit,
    XmlResolver = null,
    IgnoreWhitespace = false,
    IgnoreComments = true
  };

  private static XmlDocument? Load(ZipArchive zip, string path)
  {
    var entry = zip.GetEntry(path);
    if (entry is null)
      return null;

    var document = new XmlDocument { XmlResolver = null };
    using var reader = XmlReader.Create(OpenLimited(entry), Settings);
    document.Load(reader);
    return document;
  }

  private static Stream OpenLimited(ZipArchiveEntry entry)
  {
    if (entry.Length > MaxPartSize)
      throw new DomainValidationException("Файл Excel занадто великий");

    return entry.Open();
  }

  private static XmlNamespaceManager Namespaces(XmlDocument document)
  {
    var ns = new XmlNamespaceManager(document.NameTable);
    ns.AddNamespace("m", MainNs);
    return ns;
  }

  /// <summary>Шлях частини з відношення: відносно теки xl/ або абсолютний (/xl/...).</summary>
  private static string ResolvePath(string baseDirectory, string target)
  {
    if (target.StartsWith('/'))
      return target.TrimStart('/');

    var parts = new List<string>(baseDirectory.TrimEnd('/').Split('/'));
    foreach (var part in target.Split('/'))
    {
      if (part == "..")
      {
        if (parts.Count > 0)
          parts.RemoveAt(parts.Count - 1);
      }
      else if (part != "." && part.Length > 0)
        parts.Add(part);
    }

    return string.Join('/', parts);
  }
}
