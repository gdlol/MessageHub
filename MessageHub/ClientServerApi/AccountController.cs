using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}/account")]
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
