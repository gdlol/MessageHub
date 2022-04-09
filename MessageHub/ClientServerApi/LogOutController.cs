using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}")]
public class LogOutController : ControllerBase
{
    [Route("logout")]
    [HttpPost]
    public object LogOut() => new();

    [Route("logout/all")]
    [HttpPost]
    public object LogOutAll() => new();
}
