namespace DataCollector.Server.Domain.Exceptions;

/// <summary>
/// Порушення бізнес-правил доменної моделі (некоректний ключ, значення не відповідає типу поля тощо).
/// </summary>
public class DomainValidationException(string message) : Exception(message);
