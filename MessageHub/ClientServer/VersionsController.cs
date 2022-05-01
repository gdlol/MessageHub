using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client")]
[AllowAnonymous]
public class VersionsController : ControllerBase
{
    [Route("versions")]
    [HttpGet]
    public object GetVersions() => new
    {
        unstable_features = new object(),
        versions = new[] { "v1.2" }
    };
}
