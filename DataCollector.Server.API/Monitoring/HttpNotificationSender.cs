using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Notifications;
using Microsoft.Extensions.Options;

namespace DataCollector.Server.API.Monitoring;

/// <summary>
/// Доставка сповіщень моніторингу назовні: загальний вебхук (JSON) і/або Telegram-бот (текст).
/// Помилка будь-якого каналу - виняток: сповіщення лишається в черзі і буде надіслане повторно.
/// </summary>
public sealed class HttpNotificationSender(HttpClient http, IOptions<MonitoringOptions> options) : INotificationSender
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  private MonitoringOptions Options => options.Value;

  private bool HasWebhook => !string.IsNullOrWhiteSpace(Options.WebhookUrl);
  private bool HasTelegram => !string.IsNullOrWhiteSpace(Options.TelegramBotToken) && !string.IsNullOrWhiteSpace(Options.TelegramChatId);

  public bool IsConfigured => HasWebhook || HasTelegram;

  public async Task Send(NotificationDto notification, CancellationToken cancellationToken)
  {
    if (HasWebhook)
    {
      var body = new
      {
        @event = "monitoring.stateChanged",
        notification.Id,
        notification.RuleId,
        notification.RuleName,
        model = notification.ConfigName,
        notification.FromState,
        notification.ToState,
        notification.Value,
        notification.Threshold,
        notification.Message,
        notification.CreatedAt
      };

      using var response = await http.PostAsJsonAsync(Options.WebhookUrl, body, JsonOptions, cancellationToken);
      response.EnsureSuccessStatusCode();
    }

    if (HasTelegram)
    {
      var url = $"https://api.telegram.org/bot{Options.TelegramBotToken}/sendMessage";
      var body = new { chat_id = Options.TelegramChatId, text = TelegramText(notification), disable_web_page_preview = true };

      using var response = await http.PostAsJsonAsync(url, body, cancellationToken);
      if (!response.IsSuccessStatusCode)
      {
        // Текст помилки Telegram ("chat not found", "bot was blocked") - у журнал доставки, без токена в адресі.
        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"Telegram {(int)response.StatusCode}: {details}");
      }
    }
  }

  private string TelegramText(NotificationDto n)
  {
    var icon = n.ToState switch
    {
      "alert" => "🔴",
      "ok" => "🟢",
      _ => "⚪"
    };

    var time = n.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    var link = string.IsNullOrWhiteSpace(Options.WebUiUrl) ? "" : $"\n{Options.WebUiUrl.TrimEnd('/')}/monitoring";

    return $"{icon} {n.Message}\nМодель: {n.ConfigName} · {time}{link}";
  }
}
