using MessageHub.Complement.ReverseProxy;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/client/{version}/profile")]
[MiddlewareFilter(typeof(UserProfilePipeline))]
public class UserProfileController : ControllerBase
{
    private IActionResult UserNotFound(string userId)
    {
        return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(userId)}: {userId}"));
    }

    [Route("{userId}")]
    [HttpGet]
    public IActionResult GetProfile(string userId) => UserNotFound(userId);

    [Route("{userId}/avatar_url")]
    [HttpGet]
    public IActionResult GetAvatarUrl(string userId) => UserNotFound(userId);

    [Route("{userId}/avatar_url")]
    [HttpPut]
    public IActionResult SetAvatarUrl(string userId) => UserNotFound(userId);

    [Route("{userId}/displayname")]
    [HttpGet]
    public IActionResult GetDisplayName(string userId) => UserNotFound(userId);

    [Route("{userId}/displayname")]
    [HttpPut]
    public IActionResult SetDisplayName(string userId) => UserNotFound(userId);
}
