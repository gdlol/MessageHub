using System.Security.Claims;
using System.Text.Encodings.Web;
using MessageHub.ClientServerProtocol;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MessageHub.Authentication;

public class MatrixAuthenticationHandler : AuthenticationHandler<MatrixAuthenticationSchemeOptions>
{
    public MatrixAuthenticationHandler(
        IOptionsMonitor<MatrixAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock)
    { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? token = null;
        if (Request.Headers.TryGetValue(HeaderNames.Authorization, out var authorizationHeader))
        {
            foreach (string value in authorizationHeader)
            {
                const string prefix = "Bearer ";
                if (value.StartsWith(prefix))
                {
                    token = value[prefix.Length..].Trim();
                    break;
                }
            }
        }
        else
        {
            token = Request.Query["access_token"];
        }

        if (string.IsNullOrEmpty(token) || token != "test")
        {
            var error = MatrixError.Create(MatrixErrorCode.MissingToken);
            return Task.FromResult(AuthenticateResult.Fail(error.ToString()));
        }
        else
        {
            var claims = new[] { new Claim(ClaimTypes.Name, "token") };
            var claimsIdentity = new ClaimsIdentity(claims);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
