using EnterpriseCommerce.WebApi.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string DefaultScheme = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization") || 
            !Request.Headers["Authorization"].ToString().StartsWith(DefaultScheme))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization Header"));
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.NameIdentifier, "auth0|test-user")
        };

        if (Request.Headers.TryGetValue("X-Test-User-Id", out var userIdHeader))
        {
            claims.Add(new Claim(CustomerClaimTypes.CustomerId, userIdHeader.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-Role", out var roleHeader))
        {
            foreach (var role in roleHeader.ToString().Split(','))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
            }
        }

        if (Request.Headers.TryGetValue("X-Test-Scope", out var scopeHeader))
        {
            claims.Add(new Claim("scope", scopeHeader.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-Azp", out var azpHeader))
        {
            claims.Add(new Claim("azp", azpHeader.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-ClientId", out var clientIdHeader))
        {
            claims.Add(new Claim("client_id", clientIdHeader.ToString()));
        }

        var identity = new ClaimsIdentity(claims, DefaultScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, DefaultScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
