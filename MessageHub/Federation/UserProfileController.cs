using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class UserProfileController : ControllerBase
{
    private readonly IUserProfile userProfile;
    private readonly IPeerIdentity peerIdentity;

    public UserProfileController(IUserProfile userProfile, IPeerIdentity peerIdentity)
    {
        ArgumentNullException.ThrowIfNull(userProfile);
        ArgumentNullException.ThrowIfNull(peerIdentity);

        this.userProfile = userProfile;
        this.peerIdentity = peerIdentity;
    }

    [Route("query/profile")]
    [HttpGet]
    public async Task<IActionResult> GetProfile(
        [FromQuery(Name = "field")] string? field,
        [FromQuery(Name = "user_id")] string? userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.MissingParameter, nameof(userId)));
        }
        if (userId != UserIdentifier.FromId(peerIdentity.Id).ToString())
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }
        if (field is not null)
        {
            if (field == "displayname")
            {
                string? displayName = await userProfile.GetDisplayNameAsync(userId);
                return new JsonResult(new
                {
                    displayname = displayName
                });
            }
            else if (field == "avatar_url")
            {
                string? url = await userProfile.GetAvatarUrlAsync(userId);
                return new JsonResult(new
                {
                    avatar_url = url
                });
            }
            else
            {
                return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(field)));
            }
        }
        else
        {
            string? displayName = await userProfile.GetDisplayNameAsync(userId);
            string? url = await userProfile.GetAvatarUrlAsync(userId);
            return new JsonResult(new
            {
                displayname = displayName,
                avatar_url = url
            });
        }
    }
}
