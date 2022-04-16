using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[AllowAnonymous]
public class LogInController : ControllerBase
{
    public const string IdPId = "org.message-hub.sso";

    private readonly IAuthenticator authenticator;

    public LogInController(IAuthenticator authenticator)
    {
        this.authenticator = authenticator;
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
                            id = IdPId,
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
    [Route($"login/sso/redirect/{IdPId}")]
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
    public async Task<object> LogIn([FromBody] LogInParmeters parameters)
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
