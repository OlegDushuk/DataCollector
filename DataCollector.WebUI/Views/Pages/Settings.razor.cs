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
    ("GET", "api/data-configs/{config}/records?pageNumber=1&pageSize=10&sortBy=price", "записи з пагінацією"),
    ("POST", "api/data-configs/{config}/records", "створити запис"),
    ("PATCH", "api/data-configs/{config}/records/{id}", "оновити поля запису"),
    ("DELETE", "api/data-configs/{config}/records/{id}", "видалити запис")
  ];
}
