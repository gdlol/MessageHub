using MessageHub.Authentication;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class LogOutController : ControllerBase
{
    private readonly IAuthenticator authenticator;

    public LogOutController(IAuthenticator authenticator)
    {
        this.authenticator = authenticator;
    }

    [Route("logout")]
    [HttpPost]
    public async Task<object> LogOut()
    {
        if (Request.HttpContext.Items["token"] is string token)
        {
            string? deviceId = await authenticator.GetDeviceIdAsync(token);
            if (deviceId is not null)
            {
                await authenticator.LogOutAsync(deviceId);
            }
        }
        else
        {
            return MatrixError.Create(MatrixErrorCode.NotFound, nameof(token));
        }
        return new object();
    }

    [Route("logout/all")]
    [HttpPost]
    public async Task<object> LogOutAll()
    {
        await authenticator.LogOutAllAsync();
        return new object();
    }
}
