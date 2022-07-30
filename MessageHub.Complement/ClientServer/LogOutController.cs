using MessageHub.Complement.Authentication;
using MessageHub.Complement.ReverseProxy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = ComplementAuthenticationSchemes.Complement)]
public class LogOutController : ControllerBase
{
    [Route("logout")]
    [HttpPost]
    [MiddlewareFilter(typeof(P2pServerProxyPipeline))]
    public void LogOut() => throw new InvalidOperationException();

    [Route("logout/all")]
    [HttpPost]
    [MiddlewareFilter(typeof(P2pServerProxyPipeline))]
    public void LogOutAll() => throw new InvalidOperationException();
}
