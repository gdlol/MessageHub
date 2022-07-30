using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.Complement.Authentication;
using MessageHub.Complement.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/client/{version}/account")]
[Authorize(AuthenticationSchemes = ComplementAuthenticationSchemes.Complement)]
public class AccountController : ControllerBase
{
    private readonly Config config;
    private readonly IUserLogIn userLogIn;

    public AccountController(IUserLogIn userLogIn, Config config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(userLogIn);

        this.config = config;
        this.userLogIn = userLogIn;
    }

    [Route("whoami")]
    [HttpGet]
    public async Task<object> WhoAmI()
    {
        string userName = HttpContext.User.Identity?.Name ?? throw new InvalidOperationException();
        string userId = $"@{userName}:{config.ServerName}";
        string? deviceId = await userLogIn.TryGetDeviceIdAsync(HttpContext.GetAccessToken());
        return new WhoAmIResponse
        {
            UserId = userId,
            DeviceId = deviceId
        };
    }
}
