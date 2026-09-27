using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.Server.API.Infrastructure;

/// <summary>
/// Перетворює винятки на відповіді у форматі ProblemDetails (RFC 7807).
/// </summary>
public class ApiExceptionHandler(
  IProblemDetailsService problemDetailsService,
  ILogger<ApiExceptionHandler> logger
) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
  {
    var (status, title) = exception switch
    {
      DomainValidationException => (StatusCodes.Status400BadRequest, "Некоректні дані"),
      BadHttpRequestException => (StatusCodes.Status400BadRequest, "Некоректний запит"),
      NotFoundException => (StatusCodes.Status404NotFound, "Не знайдено"),
      ConflictException => (StatusCodes.Status409Conflict, "Конфлікт"),
      _ => (StatusCodes.Status500InternalServerError, "Внутрішня помилка сервера")
    };

    if (status == StatusCodes.Status500InternalServerError)
      logger.LogError(exception, "Unhandled exception");

    httpContext.Response.StatusCode = status;

    return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
    {
      HttpContext = httpContext,
      Exception = exception,
      ProblemDetails = new ProblemDetails
      {
        Status = status,
        Title = title,
        Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
      }
    });
  }
}
