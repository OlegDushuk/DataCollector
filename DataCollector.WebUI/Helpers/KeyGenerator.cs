using System.Text;

namespace DataCollector.WebUI.Helpers;

/// <summary>
/// Генерує ключ (латиниця, цифри, "_") з назви, зокрема з української: "Дата оплати" -> "data_oplaty".
/// </summary>
public static class KeyGenerator
{
  private const int MaxLength = 32;

  private static readonly Dictionary<char, string> Translit = new()
  {
    ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "h", ['ґ'] = "g", ['д'] = "d", ['е'] = "e", ['є'] = "ie",
    ['ж'] = "zh", ['з'] = "z", ['и'] = "y", ['і'] = "i", ['ї'] = "i", ['й'] = "i", ['к'] = "k", ['л'] = "l",
    ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
    ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shch", ['ь'] = "", ['ю'] = "iu",
    ['я'] = "ia", ['ы'] = "y", ['э'] = "e", ['ё'] = "e", ['ъ'] = "", ['\''] = "", ['’'] = "", ['ʼ'] = ""
  };

  public static string FromName(string? name)
  {
    if (string.IsNullOrWhiteSpace(name))
      return string.Empty;

    var sb = new StringBuilder();

    foreach (var ch in name.Trim().ToLowerInvariant())
    {
      if (Translit.TryGetValue(ch, out var latin))
        sb.Append(latin);
      else if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
        sb.Append(ch);
      else if (sb.Length > 0 && sb[^1] != '_')
        sb.Append('_');
    }

    var key = sb.ToString().Trim('_');

    if (key.Length > 0 && char.IsDigit(key[0]))
      key = "_" + key;

    return key.Length > MaxLength ? key[..MaxLength].TrimEnd('_') : key;
  }
}
