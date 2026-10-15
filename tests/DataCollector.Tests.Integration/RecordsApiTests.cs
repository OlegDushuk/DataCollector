using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DataCollector.Tests.Integration;

/// <summary>Записи через HTTP: створення, перевірка значень, версії (ETag / If-Match, 409), пошук, експорт, імпорт.</summary>
public class RecordsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
  private static HttpRequestMessage Patch(string url, object body, string? ifMatch)
  {
    var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body) };
    if (ifMatch != null)
      request.Headers.IfMatch.Add(new EntityTagHeaderValue(ifMatch));
    return request;
  }

  [Fact]
  public async Task Create_Get_HasVersionInBodyAndETag()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);
    var id = await Api.CreateRecord(admin, model, new { number = "ORD-1", price = "120,5" });

    var response = await admin.GetAsync($"api/data-configs/{model}/records/{id}");
    var record = await response.Content.ReadFromJsonAsync<JsonElement>();

    var version = record.GetProperty("version").GetInt64();
    Assert.Equal($"\"{version}\"", response.Headers.ETag!.Tag);
    Assert.Equal(120.5m, record.GetProperty("values").GetProperty("price").GetDecimal());
    Assert.Equal("new", record.GetProperty("values").GetProperty("status").GetString());   // значення за замовчуванням
  }

  [Fact]
  public async Task InvalidValue_400_WithUkrainianMessage()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);

    var response = await admin.PostAsJsonAsync($"api/data-configs/{model}/records", new { values = new { number = "ORD-1", price = -5 } });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Contains("Сума", (await Api.Problem(response)).GetProperty("detail").GetString());
  }

  [Fact]
  public async Task MissingRequiredField_400()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);

    var response = await admin.PostAsJsonAsync($"api/data-configs/{model}/records", new { values = new { price = 5 } });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Patch_WithStaleIfMatch_409_AndChangesNotApplied()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);
    var id = await Api.CreateRecord(admin, model, new { number = "ORD-1", price = 100 });
    var url = $"api/data-configs/{model}/records/{id}";
    var opened = (await admin.GetAsync(url)).Headers.ETag!.Tag;

    // Перший користувач зберігає зміни: нова версія - у заголовку ETag відповіді.
    var first = await admin.SendAsync(Patch(url, new { values = new { price = 150 } }, opened));
    Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
    Assert.NotEqual(opened, first.Headers.ETag!.Tag);

    // Другий - з тією ж (уже застарілою) версією.
    var second = await admin.SendAsync(Patch(url, new { values = new { price = 999 } }, opened));
    Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    Assert.Contains("змінено іншим користувачем", (await Api.Problem(second)).GetProperty("detail").GetString());

    var current = await admin.GetFromJsonAsync<JsonElement>(url);
    Assert.Equal(150m, current.GetProperty("values").GetProperty("price").GetDecimal());
  }

  [Fact]
  public async Task Patch_VersionInBody_WorksLikeIfMatch()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);
    var id = await Api.CreateRecord(admin, model, new { number = "ORD-1" });
    var url = $"api/data-configs/{model}/records/{id}";
    var version = (await admin.GetFromJsonAsync<JsonElement>(url)).GetProperty("version").GetInt64();

    var ok = await admin.SendAsync(Patch(url, new { values = new { price = 1 }, version }, null));
    var stale = await admin.SendAsync(Patch(url, new { values = new { price = 2 }, version }, null));

    Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
  }

  [Fact]
  public async Task Patch_MalformedIfMatch_400()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);
    var id = await Api.CreateRecord(admin, model, new { number = "ORD-1" });

    var request = Patch($"api/data-configs/{model}/records/{id}", new { values = new { price = 1 } }, null);
    request.Headers.TryAddWithoutValidation("If-Match", "\"not-a-version\"");

    Assert.Equal(HttpStatusCode.BadRequest, (await admin.SendAsync(request)).StatusCode);
  }

  [Fact]
  public async Task Delete_WithStaleIfMatch_409_ThenWithCurrent_204()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);
    var id = await Api.CreateRecord(admin, model, new { number = "ORD-1" });
    var url = $"api/data-configs/{model}/records/{id}";
    var opened = (await admin.GetAsync(url)).Headers.ETag!.Tag;
    var changed = (await admin.SendAsync(Patch(url, new { values = new { price = 5 } }, null))).Headers.ETag!.Tag;

    var stale = new HttpRequestMessage(HttpMethod.Delete, url);
    stale.Headers.IfMatch.Add(new EntityTagHeaderValue(opened));
    Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(stale)).StatusCode);

    var current = new HttpRequestMessage(HttpMethod.Delete, url);
    current.Headers.IfMatch.Add(new EntityTagHeaderValue(changed));
    Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(current)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(url)).StatusCode);
  }

  [Fact]
  public async Task Query_Search_ReturnsMatchingWithVersions()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);
    await Api.CreateRecord(admin, model, new { number = "ORD-ALPHA" });
    await Api.CreateRecord(admin, model, new { number = "ORD-BETA" });

    var page = await (await admin.PostAsJsonAsync($"api/data-configs/{model}/records/query", new { search = "alpha", pageSize = 10 }))
      .Content.ReadFromJsonAsync<JsonElement>();

    Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
    Assert.True(page.GetProperty("items")[0].GetProperty("version").GetInt64() > 0);
  }

  [Fact]
  public async Task Export_Csv_ReturnsFileWithRows()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);
    await Api.CreateRecord(admin, model, new { number = "=CMD()", price = 10 });

    var response = await admin.PostAsJsonAsync($"api/data-configs/{model}/records/export", new { format = "csv" });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("1", response.Headers.GetValues("X-Export-Rows").Single());
    var csv = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
    Assert.Contains("'=CMD()", csv);
  }

  [Fact]
  public async Task Import_Csv_Multipart()
  {
    var admin = await factory.SignIn();
    var model = await Api.CreateOrdersModel(admin);

    using var form = new MultipartFormDataContent();
    form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("number,price\nIMP-1,10\nIMP-2,20\n")), "file", "orders.csv");
    form.Add(new StringContent("false"), "dryRun");

    var result = await (await admin.PostAsync($"api/data-configs/{model}/records/import", form)).Content.ReadFromJsonAsync<JsonElement>();

    Assert.Equal(2, result.GetProperty("created").GetInt32());
    var page = await admin.GetFromJsonAsync<JsonElement>($"api/data-configs/{model}/records");
    Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
  }

  [Fact]
  public async Task Dashboard_SaveWithStaleVersion_409()
  {
    var editor = await factory.SignInAs("Editor");
    var created = await (await editor.PostAsJsonAsync("api/dashboards", new { name = "Продажі", widgets = Array.Empty<object>() }))
      .Content.ReadFromJsonAsync<JsonElement>();
    var url = $"api/dashboards/{created.GetProperty("id").GetGuid()}";
    var version = (await editor.GetFromJsonAsync<JsonElement>(url)).GetProperty("version").GetInt64();

    var first = await editor.PutAsJsonAsync(url, new { name = "Продажі за день", widgets = Array.Empty<object>(), version });
    var second = await editor.PutAsJsonAsync(url, new { name = "Продажі за тиждень", widgets = Array.Empty<object>(), version });

    Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
  }
}
