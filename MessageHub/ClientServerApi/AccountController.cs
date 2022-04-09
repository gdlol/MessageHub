using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}/account")]
public class AccountController : ControllerBase
{
    [Route("whoami")]
    [HttpGet]
    public object WhoAmI()
    {
        return new
        {
            user_id = Request.HttpContext.User.Identity?.Name ?? string.Empty
        };
    }
}
