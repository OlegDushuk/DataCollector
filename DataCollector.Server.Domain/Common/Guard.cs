using System.Text.RegularExpressions;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Server.Domain.Common;

internal static partial class Guard
{
  public const int MaxKeyLength = 32;
  public const int MaxNameLength = 128;

  [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
  private static partial Regex KeyRegex();

  /// <summary>
  /// Ключ використовується в API та інтеграціях, тому дозволені лише латинські літери, цифри та "_".
  /// </summary>
  public static string Key(string? key, string what)
  {
    var value = key?.Trim();

    if (string.IsNullOrEmpty(value))
      throw new DomainValidationException($"Ключ {what} не може бути порожнім");

    if (value.Length > MaxKeyLength)
      throw new DomainValidationException($"Ключ {what} не може бути довшим за {MaxKeyLength} символи");

    if (!KeyRegex().IsMatch(value))
      throw new DomainValidationException(
        $"Ключ {what} може містити лише латинські літери, цифри та '_' і не може починатися з цифри");

    return value;
  }

  /// <summary>
  /// Ключі системних полів запису: їх використовують фільтри й сортування, тому поле моделі не може так називатися.
  /// </summary>
  private static readonly string[] ReservedPropertyKeys = ["id", "createdAt", "updatedAt"];

  public static string PropertyKey(string? key)
  {
    var value = Key(key, "поля");

    if (ReservedPropertyKeys.Contains(value, StringComparer.OrdinalIgnoreCase))
      throw new DomainValidationException($"Ключ '{value}' зарезервований для системних полів запису");

    return value;
  }

  public static string Name(string? name, string what)
  {
    var value = name?.Trim();

    if (string.IsNullOrEmpty(value))
      throw new DomainValidationException($"Назва {what} не може бути порожньою");

    if (value.Length > MaxNameLength)
      throw new DomainValidationException($"Назва {what} не може бути довшою за {MaxNameLength} символів");

    return value;
  }
}
