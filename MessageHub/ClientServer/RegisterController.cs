using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
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
