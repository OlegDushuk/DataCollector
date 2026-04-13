using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Shared;

public partial class PageTemplate : ComponentBase
{
    [Parameter] public RenderFragment? MainContent { get; set; }
    [Parameter] public RenderFragment? HeaderRightContent { get; set; }
    [Parameter] public string? Title { get; set; }

    private string GetTitle => Title ?? "-";
}