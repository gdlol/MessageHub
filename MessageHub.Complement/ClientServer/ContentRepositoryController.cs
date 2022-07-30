using MessageHub.Complement.Authentication;
using MessageHub.Complement.ReverseProxy;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/media/{version}")]
[MiddlewareFilter(typeof(ContentRepositoryProxyPipeline))]
public class ContentRepositoryController : ControllerBase
{
    [Route("config")]
    [HttpGet]
    [Authorize(AuthenticationSchemes = ComplementAuthenticationSchemes.Complement)]
    public IActionResult GetConfig() => Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));

    [Route("download/{serverName}/{mediaId}")]
    [Route("download/{serverName}/{mediaId}/{fileName}")]
    [Route("thumbnail/{serverName}/{mediaId}")]
    [HttpGet]
    public IActionResult Download() => NotFound(MatrixError.Create(MatrixErrorCode.NotFound));

    [Route("upload")]
    [HttpPost]
    [Authorize(AuthenticationSchemes = ComplementAuthenticationSchemes.Complement)]
    public IActionResult Upload() => Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
}
