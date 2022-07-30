using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MessageHub.Authentication;
using MessageHub.Complement.HomeServer;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MessageHub.Complement.Authentication;

public class ComplementAuthenticationHandler : AuthenticationHandler<ComplementAuthenticationSchemeOptions>
{
    private readonly IUserLogIn userLogIn;

    public ComplementAuthenticationHandler(
        IOptionsMonitor<ComplementAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IUserLogIn userLogIn) : base(options, logger, encoder, clock)
    {
        ArgumentNullException.ThrowIfNull(userLogIn);

        this.userLogIn = userLogIn;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        AuthenticateResult Fail(MatrixError error)
        {
            Request.HttpContext.SetMatrixError(error);
            return AuthenticateResult.Fail(error.ToString());
        }

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
            return Fail(MatrixError.Create(MatrixErrorCode.MissingToken));
        }
        string? userName = await userLogIn.TryGetUserNameAsync(token);
        if (userName is null)
        {
            return Fail(MatrixError.Create(MatrixErrorCode.UnknownToken));
        }
        Request.HttpContext.SetAccessToken(token);
        var claims = new[] { new Claim(ClaimTypes.Name, userName) };
        var claimsIdentity = new ClaimsIdentity(claims, MatrixAuthenticationSchemes.Client);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        await base.HandleChallengeAsync(properties);
        if (Request.HttpContext.TryGetMatrixError(out var error))
        {
            await Response.WriteAsJsonAsync(error);
        }
    }
}
