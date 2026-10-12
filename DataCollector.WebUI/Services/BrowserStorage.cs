using System.Text.Json;
using Microsoft.JSInterop;

namespace DataCollector.WebUI.Services;

/// <summary>
/// Налаштування інтерфейсу в localStorage браузера: живуть на цьому комп'ютері в цьому браузері,
/// між сесіями і перезапусками сервера. Значення зберігаються як JSON.
/// </summary>
/// <remarks>
/// Сховище - лише зручність: якщо воно недоступне (приватний режим, заборона браузера, обірване з'єднання)
/// або збережене значення пошкоджене, читання повертає null, а запис мовчки нічого не робить -
/// сторінка працює з налаштуваннями за замовчуванням.
/// </remarks>
public sealed class BrowserStorage(IJSRuntime js)
{
  /// <summary>Спільний префікс ключів застосунку: не перетинається з іншими сайтами на тому ж хості.</summary>
  public const string Prefix = "datacollector.";

  // Кирилиця як є (а не \u0422...): збережене зручно переглядати в DevTools -> Application -> Local Storage.
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
  {
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
  };

  public async Task<string?> GetString(string key)
  {
    try
    {
      return await js.InvokeAsync<string?>("localStorage.getItem", Prefix + key);
    }
    catch (Exception e) when (IsUnavailable(e))
    {
      return null;
    }
  }

  public async Task SetString(string key, string value)
  {
    try
    {
      await js.InvokeVoidAsync("localStorage.setItem", Prefix + key, value);
    }
    catch (Exception e) when (IsUnavailable(e))
    {
      // Не вдалося зберегти - налаштування діє до перезавантаження сторінки.
    }
  }

  public async Task<T?> Get<T>(string key) where T : class
  {
    var json = await GetString(key);
    if (string.IsNullOrWhiteSpace(json))
      return null;

    try
    {
      return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
    catch (JsonException)
    {
      // Старий або пошкоджений формат - як ніби нічого не збережено.
      return null;
    }
  }

  public Task Set<T>(string key, T value) => SetString(key, Serialize(value));

  public async Task Remove(string key)
  {
    try
    {
      await js.InvokeVoidAsync("localStorage.removeItem", Prefix + key);
    }
    catch (Exception e) when (IsUnavailable(e))
    {
    }
  }

  public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

  private static bool IsUnavailable(Exception e)
    => e is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException;
}
