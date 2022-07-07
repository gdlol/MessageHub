using MessageHub.Complement.ReverseProxy;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/client/{version}/profile")]
public class UserProfileController : ControllerBase
{
    private IActionResult UserNotFound(string userId)
    {
        return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(userId)}: {userId}"));
    }

    [Route("{userId}")]
    [HttpGet]
    [MiddlewareFilter(typeof(UserProfilePipeline))]
    public IActionResult GetProfile(string userId) => UserNotFound(userId);

    [Route("{userId}/avatar_url")]
    [HttpGet]
    [MiddlewareFilter(typeof(UserProfilePipeline))]
    public IActionResult GetAvatarUrl(string userId) => UserNotFound(userId);

    [Route("{userId}/avatar_url")]
    [HttpPut]
    [MiddlewareFilter(typeof(FillJsonContentTypePipeline))]
    [MiddlewareFilter(typeof(UserProfilePipeline))]
    public IActionResult SetAvatarUrl(string userId) => UserNotFound(userId);

    [Route("{userId}/displayname")]
    [HttpGet]
    [MiddlewareFilter(typeof(UserProfilePipeline))]
    public IActionResult GetDisplayName(string userId) => UserNotFound(userId);

    [Route("{userId}/displayname")]
    [HttpPut]
    [MiddlewareFilter(typeof(FillJsonContentTypePipeline))]
    [MiddlewareFilter(typeof(UserProfilePipeline))]
    public IActionResult SetDisplayName(string userId) => UserNotFound(userId);
}
