using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Pos.Auth.WebApi.Authentication;

public class NotificationApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "NotificationServiceApiKey";
    public const string HeaderName = "X-Internal-Api-Key";
    private readonly IOptionsMonitor<NotificationApiKeyOptions> _keys;

    public NotificationApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, IOptionsMonitor<NotificationApiKeyOptions> keys)
        : base(options, logger, encoder)
    {
        _keys = keys;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        var configured = _keys.CurrentValue.ApiKey;
        var supplied = values.Count == 1 ? values[0] : null;
        if (string.IsNullOrWhiteSpace(configured) || string.IsNullOrWhiteSpace(supplied) ||
            supplied.Length > 1024)
            return Task.FromResult(AuthenticateResult.Fail("Invalid service credential."));

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash))
            return Task.FromResult(AuthenticateResult.Fail("Invalid service credential."));

        var identity = new ClaimsIdentity(new[]
        {
            new Claim("service", "NotificationService")
        }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
