namespace DataCollector.Server.API.Monitoring;

/// <summary>Налаштування моніторингу (секція "Monitoring" в appsettings).</summary>
public sealed class MonitoringOptions
{
  public const string Section = "Monitoring";

  /// <summary>Фонова перевірка правил увімкнена (вимкнути - наприклад, на другому екземплярі API).</summary>
  public bool Enabled { get; set; } = true;

  /// <summary>Як часто сервіс шукає правила, яким настав час перевірки, секунд.</summary>
  public int TickSeconds { get; set; } = 15;

  /// <summary>Загальний вебхук: POST JSON зі сповіщенням на цю адресу (порожньо - вимкнено).</summary>
  public string? WebhookUrl { get; set; }

  /// <summary>Telegram-бот: токен від @BotFather і чат (id користувача або групи), куди писати.</summary>
  public string? TelegramBotToken { get; set; }
  public string? TelegramChatId { get; set; }

  /// <summary>Адреса інтерфейсу (для посилання в повідомленні), напр. https://localhost:7243.</summary>
  public string? WebUiUrl { get; set; }
}
