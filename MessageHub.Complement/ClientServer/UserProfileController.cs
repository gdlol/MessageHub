using MessageHub.Complement.Authentication;
using MessageHub.Complement.ReverseProxy;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/client/{version}/profile")]
[Authorize(AuthenticationSchemes = ComplementAuthenticationSchemes.Complement)]
public class UserProfileController : ControllerBase
{
    private IActionResult UserNotFound(string userId)
    {
        return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(userId)}: {userId}"));
    }

    [Route("{userId}")]
    [HttpGet]
    [AllowAnonymous]
    [MiddlewareFilter(typeof(UserProfileProxyPipeline))]
    public IActionResult GetProfile(string userId) => UserNotFound(userId);

    [Route("{userId}/avatar_url")]
    [HttpGet]
    [AllowAnonymous]
    [MiddlewareFilter(typeof(UserProfileProxyPipeline))]
    public IActionResult GetAvatarUrl(string userId) => UserNotFound(userId);

    [Route("{userId}/avatar_url")]
    [HttpPut]
    [MiddlewareFilter(typeof(FillJsonContentTypePipeline))]
    [MiddlewareFilter(typeof(UserProfileProxyPipeline))]
    public IActionResult SetAvatarUrl(string userId) => UserNotFound(userId);

    [Route("{userId}/displayname")]
    [HttpGet]
    [AllowAnonymous]
    [MiddlewareFilter(typeof(UserProfileProxyPipeline))]
    public IActionResult GetDisplayName(string userId) => UserNotFound(userId);

    [Route("{userId}/displayname")]
    [HttpPut]
    [MiddlewareFilter(typeof(FillJsonContentTypePipeline))]
    [MiddlewareFilter(typeof(UserProfileProxyPipeline))]
    public IActionResult SetDisplayName(string userId) => UserNotFound(userId);
}
