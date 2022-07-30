using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}/account")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class AccountController : ControllerBase
{
    private readonly IAuthenticator authenticator;

    public AccountController(IAuthenticator authenticator)
    {
        ArgumentNullException.ThrowIfNull(authenticator);

        this.authenticator = authenticator;
    }

    [Route("whoami")]
    [HttpGet]
    public async Task<WhoAmIResponse> WhoAmI()
    {
        string userId = HttpContext.User.Identity?.Name ?? throw new InvalidOperationException();
        string? deviceId = await authenticator.GetDeviceIdAsync(HttpContext.GetAccessToken());
        return new WhoAmIResponse
        {
            UserId = userId,
            DeviceId = deviceId
        };
    }
}
