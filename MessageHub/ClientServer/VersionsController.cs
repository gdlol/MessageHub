using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client")]
public class VersionsController : ControllerBase
{
    [Route("versions")]
    [HttpGet]
    public object GetVersions() => new
    {
        unstable_features = new object(),
        versions = new[] { "r0.6.0", "v1.2", "v1.3" }
    };
}
