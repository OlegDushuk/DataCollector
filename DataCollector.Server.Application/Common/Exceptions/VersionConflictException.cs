namespace DataCollector.Server.Application.Common.Exceptions;

/// <summary>
/// Оптимістична конкурентність: об'єкт змінили після того, як клієнт його прочитав
/// (версія, яку надіслав клієнт, не збігається з поточною). Відповідь - 409.
/// </summary>
public class VersionConflictException(string message) : ConflictException(message)
{
  public const string Record = "Запис змінено іншим користувачем після того, як ви його відкрили. Оновіть дані й повторіть зміну";
  public const string Dashboard = "Дашборд змінено в іншому вікні або іншим користувачем після того, як ви його відкрили. Перезавантажте дашборд і повторіть зміни";
  public const string Rule = "Правило змінено іншим користувачем після того, як ви його відкрили. Оновіть сторінку й повторіть зміни";

  /// <summary>
  /// Версія, яку бачив клієнт, має збігатися з поточною. null - клієнт версію не надіслав,
  /// перевірки немає (останнє збереження перемагає) - так працюють прості інтеграції.
  /// </summary>
  public static void Ensure(long? expected, long actual, string message)
  {
    if (expected is { } version && version != actual)
      throw new VersionConflictException(message);
  }
}
