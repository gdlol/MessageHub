using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MessageHub.Authentication;

public class ClientAuthenticationHandler : AuthenticationHandler<ClientAuthenticationSchemeOptions>
{
    private readonly IAuthenticator authenticator;

    public ClientAuthenticationHandler(
        IOptionsMonitor<ClientAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IAuthenticator authenticator) : base(options, logger, encoder, clock)
    {
        ArgumentNullException.ThrowIfNull(authenticator);

        this.authenticator = authenticator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? token = null;
        if (Request.Headers.TryGetValue(HeaderNames.Authorization, out var authorizationHeader))
        {
            foreach (string value in authorizationHeader)
            {
                if (AuthenticationHeaderValue.TryParse(value, out var header) && header.Scheme == "Bearer")
                {
                    token = header.Parameter;
                }
            }
        }
        else
        {
            token = Request.Query["access_token"];
        }

        if (string.IsNullOrEmpty(token))
        {
            var error = MatrixError.Create(MatrixErrorCode.MissingToken);
            return AuthenticateResult.Fail(error.ToString());
        }
        else
        {
            string? userId = await authenticator.AuthenticateAsync(token);
            if (userId is null)
            {
                var error = MatrixError.Create(MatrixErrorCode.UnknownToken);
                return AuthenticateResult.Fail(error.ToString());
            }
            Request.HttpContext.Items[nameof(token)] = token;
            var claims = new[] { new Claim(ClaimTypes.Name, userId) };
            var claimsIdentity = new ClaimsIdentity(claims, MatrixAuthenticationSchemes.Client);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
    }
}
