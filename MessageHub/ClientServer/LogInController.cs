using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events.General;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
public class LogInController : ControllerBase
{
    private readonly IAuthenticator authenticator;
    private readonly IUserPresence userPresence;

    public LogInController(IAuthenticator authenticator, IUserPresence userPresence)
    {
        this.authenticator = authenticator;
        this.userPresence = userPresence;
    }

    [Route("login")]
    [HttpGet]
    public object LogIn()
    {
        return new
        {
            flows = new object[]
            {
                new
                {
                    identity_providers = new[]
                    {
                        new
                        {
                            id = nameof(MessageHub),
                            name = nameof(MessageHub)
                        }
                    },
                    type = "m.login.sso"
                },
                new { type = "m.login.token" }
            }
        };
    }

    [Route("login/sso/redirect")]
    [Route("login/sso/redirect/{IdPId}")]
    [HttpGet]
    public IActionResult LogInSsoRedirect()
    {
        string redirectUrl;
        redirectUrl = Request.Query[nameof(redirectUrl)];
        string ssoRedirectUrl = authenticator.GetSsoRedirectUrl(redirectUrl);
        return Redirect(ssoRedirectUrl);
    }

    [Route("login")]
    [HttpPost]
    public async Task<object> LogIn([FromBody] LogInParameters parameters)
    {
        if (parameters is null)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.InvalidParameter));
        }
        if (parameters.LogInType != "m.login.token")
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(parameters.LogInType)));
        }
        if (parameters.Token is null)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.MissingToken));
        }

        string deviceId = parameters.DeviceId ?? Guid.NewGuid().ToString();
        var result = await authenticator.LogInAsync(deviceId, parameters.Token);
        if (result is (string userId, string accessToken))
        {
            var presenceStatus = userPresence.GetPresence(userId);
            if (presenceStatus is null)
            {
                userPresence.SetPresence(userId, PresenceValues.Online, null);
            }
            return new
            {
                access_token = accessToken,
                device_id = deviceId,
                user_id = userId
            };
        }
        return BadRequest(MatrixError.Create(MatrixErrorCode.UnknownToken));
    }
}
