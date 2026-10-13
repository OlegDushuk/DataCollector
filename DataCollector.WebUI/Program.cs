using DataCollector.WebUI.ServerApiServices;
using DataCollector.WebUI.Views;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
  .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["ServerApi:BaseUrl"]
                 ?? throw new InvalidOperationException("ServerApi:BaseUrl is not configured");

builder.Services.AddScoped<DataCollector.WebUI.Services.BrowserStorage>();
builder.Services.AddScoped<DataCollector.WebUI.Services.LayoutState>();
builder.Services.AddHttpClient<EntityConfigApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<DashboardApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<EntityRecordApiService>(c =>
{
  c.BaseAddress = new Uri(apiBaseUrl);

  // Експорт до 100 000 записів може тривати довше за стандартні 100 с. Звичайні запити таблиці
  // від цього не «зависають»: сторінка скасовує їх сама, коли користувач іде далі.
  c.Timeout = TimeSpan.FromMinutes(5);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
  // Непередбачена помилка - сторінка /error замість порожньої відповіді (у Development - детальна сторінка).
  app.UseExceptionHandler("/error", createScopeForErrors: true);
  app.UseHsts();
}

// Невідома адреса при першому відкритті - сторінка «Не знайдено» з меню, а не порожня 404 браузера.
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
  .AddInteractiveServerRenderMode();

app.Run();
