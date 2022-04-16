using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[AllowAnonymous]
public class VersionController : ControllerBase
{
    [Route("version")]
    [HttpGet]
    public object GetVersion() => new
    {
        name = nameof(MessageHub),
        version = "v0.1"
    };
}
