using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DataCollector.Tests.Integration;

internal static class Api
{
  /// <summary>Модель «Замовлення» з унікальним ключем (тести в класі ділять одне сховище).</summary>
  public static async Task<string> CreateOrdersModel(HttpClient admin)
  {
    var key = "orders_" + Guid.NewGuid().ToString("N")[..8];
    var response = await admin.PostAsJsonAsync("api/data-configs", new
    {
      name = "Замовлення",
      key,
      properties = new object[]
      {
        new { name = "Номер", key = "number", dataType = "Text", rules = new { isRequired = true } },
        new { name = "Сума", key = "price", dataType = "Number", rules = new { min = 0 } },
        new { name = "Статус", key = "status", dataType = "Enum", rules = new { options = new[] { "new", "done" }, defaultValue = "new" } }
      }
    });
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return key;
  }

  public static async Task<Guid> CreateRecord(HttpClient client, string model, object values)
  {
    var response = await client.PostAsJsonAsync($"api/data-configs/{model}/records", new { values });
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  }

  /// <summary>Тіло помилки у форматі Problem Details (RFC 9457).</summary>
  public static async Task<JsonElement> Problem(HttpResponseMessage response)
  {
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    return await response.Content.ReadFromJsonAsync<JsonElement>();
  }
}
