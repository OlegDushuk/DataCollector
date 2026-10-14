using System.Security.Claims;
using DataCollector.Server.API.Auth;
using Serilog.AspNetCore;
using Serilog.Events;

namespace DataCollector.Server.API.Infrastructure;

/// <summary>
/// Журнал HTTP-запитів: один запис на запит з методом, адресою, кодом відповіді, тривалістю
/// і тим, хто виконав запит (користувач або API-ключ), а також IP клієнта.
/// </summary>
/// <remarks>
/// Рівні: помилки сервера (5xx) - Error; відмови (4xx) і повільні запити (понад 2 с) - Warning;
/// зміни (POST/PUT/PATCH/DELETE) - Information; успішне читання (GET) - Debug, щоб опитування
/// інтерфейсу (лічильник сповіщень, автооновлення дашбордів) не засмічувало журнал.
/// </remarks>
public static class RequestLogging
{
  private const double SlowRequestMs = 2000;

  public static void Configure(RequestLoggingOptions options)
  {
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} -> {StatusCode} за {Elapsed:0} мс ({UserName})";

    options.GetLevel = (context, elapsed, exception) =>
    {
      var status = context.Response.StatusCode;

      if (exception != null || status >= 500)
        return LogEventLevel.Error;

      if (status >= 400 || elapsed > SlowRequestMs)
        return LogEventLevel.Warning;

      return HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
        ? LogEventLevel.Debug
        : LogEventLevel.Information;
    };

    options.EnrichDiagnosticContext = (diagnostics, context) =>
    {
      var user = context.User;
      diagnostics.Set("UserName", user.Identity?.IsAuthenticated == true ? user.Identity.Name : "anonymous");
      diagnostics.Set("AuthKind", user.FindFirstValue(AuthClaims.Kind) ?? "none");
      diagnostics.Set("ClientIp", context.Connection.RemoteIpAddress?.ToString());
      diagnostics.Set("RequestQuery", context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null);
    };
  }
}
