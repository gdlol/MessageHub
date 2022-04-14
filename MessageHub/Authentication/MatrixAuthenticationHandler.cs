using System.Security.Claims;
using System.Text.Encodings.Web;
using MessageHub.ClientServerProtocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MessageHub.Authentication;

public class MatrixAuthenticationHandler : AuthenticationHandler<MatrixAuthenticationSchemeOptions>
{
    private readonly IAuthenticator authenticator;

    public MatrixAuthenticationHandler(
        IOptionsMonitor<MatrixAuthenticationSchemeOptions> options,
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
            var claimsIdentity = new ClaimsIdentity(claims, MatrixDefaults.AuthenticationScheme);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
    }
}
