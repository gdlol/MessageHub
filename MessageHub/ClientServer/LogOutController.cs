using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
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
            throw new InvalidOperationException();
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
