using DataCollector.WebUI.ServerApiServices;
using DataCollector.WebUI.Views;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
  .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["ServerApi:BaseUrl"]
                 ?? throw new InvalidOperationException("ServerApi:BaseUrl is not configured");

builder.Services.AddHttpClient<EntityConfigApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<EntityRecordApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));

var app = builder.Build();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
  .AddInteractiveServerRenderMode();

app.Run();
