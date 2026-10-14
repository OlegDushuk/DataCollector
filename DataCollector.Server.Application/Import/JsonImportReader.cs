using System.Text.Json;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Application.Import;

/// <summary>
/// JSON: масив об'єктів <c>[{ "ключ": значення }]</c> або файл експорту <c>{ "rows": [...] }</c>
/// (також приймаються "records", "items", "data"). Колонки - усі ключі об'єктів у порядку появи.
/// </summary>
internal sealed class JsonImportReader : IImportReader
{
  private static readonly string[] RowProperties = ["rows", "records", "items", "data"];

  public ImportTable Read(Stream input, int maxRows)
  {
    JsonDocument document;
    try
    {
      document = JsonDocument.Parse(input, new JsonDocumentOptions
      {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 32
      });
    }
    catch (JsonException e)
    {
      throw new DomainValidationException($"JSON пошкоджений: {e.Message}");
    }

    using (document)
    {
      var array = RowsArray(document.RootElement);
      if (array.GetArrayLength() > maxRows)
        throw ImportFormats.TooManyRows(maxRows);

      var headers = new List<string>();
      var index = new Dictionary<string, int>(StringComparer.Ordinal);
      var objects = new List<(int Number, Dictionary<int, string?> Values)>();
      var number = 0;

      foreach (var item in array.EnumerateArray())
      {
        number++;
        if (item.ValueKind != JsonValueKind.Object)
          throw new DomainValidationException($"Елемент {number} масиву не є об'єктом");

        var values = new Dictionary<int, string?>();
        foreach (var property in item.EnumerateObject())
        {
          if (!index.TryGetValue(property.Name, out var column))
          {
            column = headers.Count;
            index[property.Name] = column;
            headers.Add(property.Name);
          }

          values[column] = Text(property.Value);
        }

        objects.Add((number, values));
      }

      var rows = objects
        .Where(o => o.Values.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
        .Select(o => new ImportRow(o.Number,
          Enumerable.Range(0, headers.Count).Select(i => o.Values.GetValueOrDefault(i)).ToArray()))
        .ToList();

      return new ImportTable { Headers = headers, Rows = rows };
    }
  }

  private static JsonElement RowsArray(JsonElement root)
  {
    if (root.ValueKind == JsonValueKind.Array)
      return root;

    if (root.ValueKind == JsonValueKind.Object)
    {
      foreach (var name in RowProperties)
      {
        foreach (var property in root.EnumerateObject())
        {
          if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
              property.Value.ValueKind == JsonValueKind.Array)
            return property.Value;
        }
      }
    }

    throw new DomainValidationException("Очікується масив об'єктів або об'єкт із масивом \"rows\"");
  }

  /// <summary>Значення як текст: числа - як записані, true/false, null - порожньо, об'єкти й масиви - JSON.</summary>
  private static string? Text(JsonElement value) => value.ValueKind switch
  {
    JsonValueKind.String => value.GetString(),
    JsonValueKind.Number => value.GetRawText(),
    JsonValueKind.True => "true",
    JsonValueKind.False => "false",
    JsonValueKind.Null or JsonValueKind.Undefined => null,
    _ => value.GetRawText()
  };
}
