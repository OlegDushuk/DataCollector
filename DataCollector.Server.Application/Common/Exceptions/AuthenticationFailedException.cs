namespace DataCollector.Server.Application.Common.Exceptions;

/// <summary>Невдалий вхід: невірний логін чи пароль, обліковий запис вимкнено або тимчасово заблоковано (HTTP 401).</summary>
public class AuthenticationFailedException(string message) : Exception(message);
