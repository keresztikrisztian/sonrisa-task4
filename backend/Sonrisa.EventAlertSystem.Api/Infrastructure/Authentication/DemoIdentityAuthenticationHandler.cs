using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Sonrisa.EventAlertSystem.Api.Application;

namespace Sonrisa.EventAlertSystem.Api.Infrastructure.Authentication;

public sealed class DemoIdentityAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    DemoIdentityRegistry identityRegistry)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DemoIdentity";
    private const string IdentityHeaderName = "X-Demo-Identity";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identityId = Request.Headers[IdentityHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(identityId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!identityRegistry.TryGet(identityId, out var identity) || identity is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown demo identity."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, identity.Id),
            new Claim(ClaimTypes.Name, identity.DisplayName),
            new Claim(ClaimTypes.Role, identity.Role)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}