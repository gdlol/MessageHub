using System.Web;
using MessageHub.ClientServerProtocol;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}")]
[AllowAnonymous]
public class LogInController : ControllerBase
{
    [Route("login")]
    [HttpGet]
    public object LogIn()
    {
        return new
        {
            flows = new object[]
            {
                new { type = "m.login.sso" },
                new { type = "m.login.token" }
            }
        };
    }

    [Route("login/sso/redirect")]
    [HttpGet]
    public IActionResult LogInSsoRedirect()
    {
        string redirectUrl;
        redirectUrl = Request.Query[nameof(redirectUrl)];
        var uriBuilder = new UriBuilder(redirectUrl);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["loginToken"] = "test";
        uriBuilder.Query = query.ToString();
        return Redirect(uriBuilder.ToString());
    }

    [Route("login")]
    [HttpPost]
    public object LogIn([FromBody] LogInParmeters parameters)
    {
        if (parameters.LogInType != "m.login.token")
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(parameters.LogInType)));
        }

        return new
        {
            access_token = parameters.Token,
            device_id = parameters.DeviceId is null ? Guid.NewGuid().ToString() : parameters.DeviceId,
            user_id = parameters.Token
        };
    }
}
