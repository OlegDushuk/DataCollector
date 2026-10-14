using System.Text.Encodings.Web;
using DataCollector.Server.Application.ExternalContracts.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DataCollector.Server.API.Auth;

/// <summary>
/// Автентифікація зовнішніх систем за заголовком <c>X-Api-Key</c>. Ключ шукається за SHA-256 (з кешем на хвилину),
/// відкликаний або невідомий ключ - 401.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
  IOptionsMonitor<AuthenticationSchemeOptions> options,
  ILoggerFactory loggerFactory,
  UrlEncoder encoder,
  IApiKeyService apiKeys
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.TryGetValue(AuthSchemes.ApiKeyHeader, out var values))
      return AuthenticateResult.NoResult();

    var plain = values.ToString().Trim();
    var identity = await apiKeys.Authenticate(plain);

    if (identity is null)
    {
      // У журнал - лише початок ключа: повний ключ у журналі був би витоком.
      Logger.LogWarning("Відхилено невідомий або відкликаний API-ключ {Prefix}…",
        plain.Length > 10 ? plain[..10] : plain);
      return AuthenticateResult.Fail("Невідомий або відкликаний API-ключ");
    }

    var principal = AuthClaims.ForApiKey(identity);
    return AuthenticateResult.Success(new AuthenticationTicket(principal, AuthSchemes.ApiKey));
  }
}
