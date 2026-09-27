namespace DataCollector.Server.Application.Common.Exceptions;

/// <summary>
/// Конфлікт з даними, що вже існують (наприклад, ключ моделі зайнятий).
/// </summary>
public class ConflictException(string message) : Exception(message);
