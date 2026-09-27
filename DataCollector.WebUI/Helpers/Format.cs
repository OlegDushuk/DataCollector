using DataCollector.WebUI.Enums;

namespace DataCollector.WebUI.Helpers;

public static class Format
{
  public static string Date(DateTime value)
    => value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

  public static string Date(DateTime? value)
    => value.HasValue ? Date(value.Value) : "—";

  public static string DataType(PropertyDataType type) => type switch
  {
    PropertyDataType.Text => "Текст",
    PropertyDataType.Number => "Число",
    PropertyDataType.Boolean => "Так / Ні",
    _ => type.ToString()
  };
}
