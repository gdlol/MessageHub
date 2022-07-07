using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/complement")]
public class ComplementController : ControllerBase
{
    [Route("loginToken")]
    [HttpGet]
    public string? GetLoginToken([FromQuery] string? loginToken)
    {
        return loginToken;
    }
}
