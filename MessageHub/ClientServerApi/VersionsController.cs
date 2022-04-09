using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client")]
[AllowAnonymous]
public class VersionsController : ControllerBase
{
    [Route("versions")]
    public object GetVersions() => new
    {
        unstable_features = new object(),
        versions = new[] { "r0.6.0", "v1.2" }
    };
}
