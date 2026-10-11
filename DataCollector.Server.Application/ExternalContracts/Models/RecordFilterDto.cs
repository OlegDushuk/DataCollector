using System.Text.Json;

namespace DataCollector.Server.Application.ExternalContracts.Models;

/// <summary>
/// Умова фільтра. Значення передаються так само, як значення записів (число, рядок, true/false, дата).
/// </summary>
/// <remarks>
/// Оператори (<see cref="Operator"/>):
/// <list type="bullet">
///   <item><c>eq</c>, <c>neq</c> - дорівнює / не дорівнює (усі типи);</item>
///   <item><c>gt</c>, <c>gte</c>, <c>lt</c>, <c>lte</c>, <c>between</c> - числа, дати, createdAt / updatedAt;</item>
///   <item><c>contains</c>, <c>notContains</c>, <c>startsWith</c>, <c>endsWith</c> - текст;</item>
///   <item><c>in</c>, <c>notIn</c> - текст і перелік, значення в <see cref="Values"/>;</item>
///   <item><c>isEmpty</c>, <c>isNotEmpty</c> - усі поля моделі та updatedAt, без значення.</item>
/// </list>
/// </remarks>
public class RecordFilterDto
{
  /// <summary>Ключ поля моделі, "createdAt" або "updatedAt".</summary>
  public string Field { get; set; } = null!;

  public string Operator { get; set; } = null!;

  /// <summary>Значення; для <c>between</c> - початок діапазону.</summary>
  public JsonElement? Value { get; set; }

  /// <summary>Кінець діапазону для <c>between</c> (включно).</summary>
  public JsonElement? ValueTo { get; set; }

  /// <summary>Список значень для <c>in</c> / <c>notIn</c>.</summary>
  public List<JsonElement>? Values { get; set; }
}
