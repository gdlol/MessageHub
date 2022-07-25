using MessageHub.Complement.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/client/{version}/account")]
[Authorize(AuthenticationSchemes = ComplementAuthenticationSchemes.Complement)]
public class AccountController : ControllerBase
{
    [Route("whoami")]
    [HttpGet]
    public object WhoAmI()
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        return new { user_id = userId };
    }
}
