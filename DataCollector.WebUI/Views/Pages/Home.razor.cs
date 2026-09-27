using DataCollector.WebUI.Entities;
using DataCollector.WebUI.ServerApiServices;
using DataCollector.WebUI.Views.Base;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Pages;

public partial class Home : PageBase
{
  [Inject] private EntityConfigApiService ConfigApi { get; set; } = null!;

  private List<EntityConfigListItem>? _configs;

  protected override async Task OnInitializedAsync()
  {
    await Try(async () => _configs = await ConfigApi.GetAll());
  }
}
