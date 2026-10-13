using DataCollector.WebUI.Views.Components.DataTable.Enums;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Components.DataTable.Models;

/// <summary>
/// Опис колонки. Оновлюється з <c>ColumnTemplate</c> при кожній зміні параметрів.
/// </summary>
public class Column<TItem>
{
  public RenderFragment<TItem> Template { get; set; } = _ => _ => { };
  public string? Key { get; set; }
  public string Title { get; set; } = string.Empty;
  public bool IsSortable { get; set; }
  public bool IsFilterable { get; set; }
  public DataTableFilterType FilterType { get; set; }

  /// <summary>Фіксована ширина (наприклад "120px"); без неї колонка розтягується.</summary>
  public string? Width { get; set; }

  /// <summary>Позиція колонки; однакові - у порядку появи. Дає змінювати порядок без перебудови таблиці.</summary>
  public int Order { get; set; }

  /// <summary>Колонка закріплена праворуч і видна при горизонтальній прокрутці (наприклад, кнопки дій).</summary>
  public bool Sticky { get; set; }

  public string CssClass => Sticky ? "data-table__cell--sticky" : string.Empty;

  public bool CanSort => IsSortable && !string.IsNullOrEmpty(Key);

  public string Style => Width is null ? string.Empty : $"flex: 0 0 {Width};";

  /// <summary>Скільки місця колонка займає щонайменше: фіксована ширина в px або типова мінімальна.</summary>
  public int MinWidthPx(int defaultWidth)
    => Width != null && Width.EndsWith("px", StringComparison.Ordinal) &&
       int.TryParse(Width[..^2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var px)
      ? px
      : defaultWidth;
}
