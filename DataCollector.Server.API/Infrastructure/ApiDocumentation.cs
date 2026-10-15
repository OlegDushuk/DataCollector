using DataCollector.Server.API.Auth;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace DataCollector.Server.API.Infrastructure;

/// <summary>
/// Опис API у форматі OpenAPI 3.1 (/openapi/v1.json) та інтерактивна документація Scalar (/scalar),
/// де запити можна виконувати з токеном або API-ключем. У Development - без входу.
/// </summary>
public static class ApiDocumentation
{
  private const string BearerScheme = "Bearer";
  private const string ApiKeyScheme = "ApiKey";

  public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
  {
    return services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
    {
      document.Info = new OpenApiInfo
      {
        Title = "DataCollector API",
        Version = "v1",
        Description = "Система зберігання, моніторингу та аналізу даних: моделі даних користувача, записи, " +
                      "пошук і агрегація, імпорт і експорт, дашборди, правила моніторингу. " +
                      "Автентифікація - токен з POST api/auth/login (Authorization: Bearer …) " +
                      "або ключ зовнішньої системи (X-Api-Key). Помилки - у форматі Problem Details (RFC 9457)."
      };

      // Дві схеми автентифікації; кожна вимога окремо - тобто достатньо будь-якої з них.
      document.Components ??= new OpenApiComponents();
      document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
      document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
      {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Токен користувача з POST api/auth/login"
      };
      document.Components.SecuritySchemes[ApiKeyScheme] = new OpenApiSecurityScheme
      {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = AuthSchemes.ApiKeyHeader,
        Description = "API-ключ зовнішньої системи (створює адміністратор)"
      };

      document.Security =
      [
        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [] },
        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(ApiKeyScheme, document)] = [] }
      ];

      return Task.CompletedTask;
    }));
  }

  public static void MapApiDocumentation(this IEndpointRouteBuilder endpoints)
  {
    // Специфікація: /openapi/v1.json; інтерактивна документація: /scalar.
    endpoints.MapOpenApi().AllowAnonymous();
    endpoints.MapScalarApiReference(options => options
        .WithTitle("DataCollector API")
        .AddPreferredSecuritySchemes(BearerScheme))
      .AllowAnonymous();
  }
}
