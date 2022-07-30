using MessageHub.Complement.Authentication;
using MessageHub.Complement.ReverseProxy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = ComplementAuthenticationSchemes.Complement)]
public class SyncController
{
    [Route("sync")]
    [HttpGet]
    [MiddlewareFilter(typeof(P2pServerProxyPipeline))]
    public Task<IActionResult> Sync() => throw new NotImplementedException();
}
