using DataCollector.WebUI.Views.Base;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Pages;

public partial class Settings : PageBase
{
  [Inject] private IConfiguration Configuration { get; set; } = null!;

  private string ApiBaseUrl => Configuration["ServerApi:BaseUrl"] ?? "—";

  private static readonly (string Method, string Path, string Description)[] Endpoints =
  [
    ("GET", "api/data-configs", "список моделей"),
    ("GET", "api/data-configs/{config}", "модель з полями"),
    ("GET", "api/data-configs/{config}/records?pageNumber=1&pageSize=10&sortBy=price&search=текст&fields=price", "записи з пагінацією, пошуком і вибором полів"),
    ("POST", "api/data-configs/{config}/records/query", "записи з умовами фільтра"),
    ("POST", "api/data-configs/{config}/records/aggregate", "групування і показники (кількість, сума, середнє...)"),
    ("POST", "api/data-configs/{config}/records/export", "файл із записами: csv, xlsx, json, xml"),
    ("POST", "api/data-configs/{config}/records/aggregate/export", "файл зі зведенням"),
    ("POST", "api/data-configs/{config}/records", "створити запис"),
    ("PATCH", "api/data-configs/{config}/records/{id}", "оновити поля запису"),
    ("DELETE", "api/data-configs/{config}/records/{id}", "видалити запис")
  ];
}
