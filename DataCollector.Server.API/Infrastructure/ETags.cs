namespace DataCollector.Server.API.Infrastructure;

/// <summary>
/// Версія об'єкта в HTTP-заголовках (оптимістична конкурентність, RFC 9110): GET повертає її в ETag,
/// клієнт надсилає назад в If-Match при зміні чи видаленні. Альтернатива - поле version у тілі запиту.
/// </summary>
public static class ETags
{
  public static void Set(HttpResponse response, long version)
  {
    response.Headers.ETag = $"\"{version}\"";
  }

  /// <summary>Версія з If-Match: "15" або W/"15"; null - заголовка немає або це «*» (будь-яка версія).</summary>
  public static long? IfMatch(HttpRequest request)
  {
    var value = request.Headers.IfMatch.ToString().Trim();

    if (value.Length == 0 || value == "*")
      return null;

    if (value.StartsWith("W/", StringComparison.Ordinal))
      value = value[2..];

    return long.TryParse(value.Trim('"'), out var version)
      ? version
      : throw new BadHttpRequestException("Заголовок If-Match має містити одну версію у форматі ETag (\"15\")");
  }
}
