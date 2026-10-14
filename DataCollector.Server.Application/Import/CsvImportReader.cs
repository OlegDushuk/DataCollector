using System.Text;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Application.Import;

/// <summary>
/// CSV (RFC 4180): значення в лапках можуть містити роздільник, лапки ("") і переноси рядків.
/// Роздільник визначається за рядком заголовків: кома, крапка з комою (Excel з українськими
/// регіональними налаштуваннями) або табуляція.
/// </summary>
/// <remarks>
/// Кодування: UTF-8 (з BOM або без); якщо файл не є коректним UTF-8 - Windows-1251
/// (так зберігає «CSV (розділювач - кома)» Excel на українській Windows).
/// </remarks>
internal sealed class CsvImportReader : IImportReader
{
  private static readonly char[] Separators = [',', ';', '\t'];

  static CsvImportReader()
  {
    // Windows-1251 та інші кодові сторінки в .NET доступні лише через провайдер.
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
  }

  public ImportTable Read(Stream input, int maxRows)
  {
    var text = Decode(input);
    var separator = DetectSeparator(text);

    // Ліміт - рядки даних плюс рядок заголовків.
    var records = Parse(text, separator, maxRows + 1);
    var header = records.FirstOrDefault()
                 ?? throw new DomainValidationException("Файл порожній: немає рядка заголовків");

    var rows = records.Skip(1).ToList();

    return new ImportTable
    {
      Headers = header.Cells.Select(c => c?.Trim() ?? string.Empty).ToList(),
      Rows = rows
    };
  }

  internal static string Decode(Stream input)
  {
    using var memory = new MemoryStream();
    input.CopyTo(memory);
    var bytes = memory.ToArray();

    if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
      return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

    if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
      return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

    try
    {
      return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
    }
    catch (DecoderFallbackException)
    {
      return Encoding.GetEncoding(1251).GetString(bytes);
    }
  }

  /// <summary>Роздільник, якого найбільше в першому рядку поза лапками (за рівності - кома).</summary>
  internal static char DetectSeparator(string text)
  {
    var counts = new int[Separators.Length];
    var quoted = false;

    foreach (var c in text)
    {
      if (c == '"')
        quoted = !quoted;
      else if (!quoted && c is '\r' or '\n')
        break;
      else if (!quoted)
      {
        var index = Array.IndexOf(Separators, c);
        if (index >= 0)
          counts[index]++;
      }
    }

    var best = 0;
    for (var i = 1; i < counts.Length; i++)
    {
      if (counts[i] > counts[best])
        best = i;
    }

    return Separators[best];
  }

  /// <summary>Записи CSV з номером рядка файлу, з якого запис починається.</summary>
  internal static List<ImportRow> Parse(string text, char separator, int maxRecords)
  {
    var records = new List<ImportRow>();
    var cells = new List<string?>();
    var cell = new StringBuilder();
    var quoted = false;
    var cellWasQuoted = false;
    var line = 1;
    var recordLine = 1;

    void EndCell()
    {
      var value = cell.ToString();
      cells.Add(value.Length == 0 && !cellWasQuoted ? null : value);
      cell.Clear();
      cellWasQuoted = false;
    }

    void EndRecord()
    {
      EndCell();

      // Порожні рядки (між записами, у кінці файлу) пропускаються.
      if (cells.Any(c => !string.IsNullOrWhiteSpace(c)))
        records.Add(new ImportRow(recordLine, cells.ToArray()));
      cells.Clear();

      // Перевищення ліміту - далі не розбираємо: файл однаково буде відхилено.
      if (records.Count > maxRecords)
        throw ImportFormats.TooManyRows(maxRecords - 1);
    }

    for (var i = 0; i < text.Length; i++)
    {
      var c = text[i];

      if (quoted)
      {
        if (c == '"')
        {
          if (i + 1 < text.Length && text[i + 1] == '"')
          {
            cell.Append('"');
            i++;
          }
          else
          {
            quoted = false;
          }
        }
        else
        {
          if (c == '\n')
            line++;
          cell.Append(c);
        }

        continue;
      }

      if (c == '"' && cell.Length == 0)
      {
        quoted = true;
        cellWasQuoted = true;
      }
      else if (c == separator)
      {
        EndCell();
      }
      else if (c is '\r' or '\n')
      {
        if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
          i++;

        EndRecord();
        line++;
        recordLine = line;
      }
      else
      {
        cell.Append(c);
      }
    }

    if (quoted)
      throw new DomainValidationException($"CSV пошкоджений: лапки, відкриті в рядку {recordLine}, не закрито");

    if (cell.Length > 0 || cells.Count > 0 || cellWasQuoted)
      EndRecord();

    return records;
  }
}
