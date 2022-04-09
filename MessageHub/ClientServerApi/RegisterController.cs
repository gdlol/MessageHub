using MessageHub.ClientServerProtocol;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}")]
[AllowAnonymous]
public class RegisterController : ControllerBase
{
    [Route("register")]
    [HttpPost]
    public IActionResult Register()
    {
        return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
