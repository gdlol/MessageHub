using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Notifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}/profile")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class UserProfileController : ControllerBase
{
    private readonly IIdentityService identityService;
    private readonly IUserProfile userProfile;
    private readonly UserProfileUpdateNotifier notifier;

    public UserProfileController(
        IIdentityService identityService,
        IUserProfile userProfile,
        UserProfileUpdateNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(userProfile);
        ArgumentNullException.ThrowIfNull(notifier);

        this.identityService = identityService;
        this.userProfile = userProfile;
        this.notifier = notifier;
    }

    private bool CheckUserId(string userId)
    {
        if (!identityService.HasSelfIdentity)
        {
            return false;
        }
        return UserIdentifier.FromId(identityService.GetSelfIdentity().Id).ToString() == userId;
    }

    private IActionResult UserNotFound(string userId)
    {
        return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(userId)}: {userId}"));
    }

    [Route("{userId}")]
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetProfile(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }
        if (!CheckUserId(userId))
        {
            return UserNotFound(userId);
        }

        string? avatarUrl = await userProfile.GetAvatarUrlAsync(userId);
        string? displayName = await userProfile.GetDisplayNameAsync(userId);
        return new JsonResult(new
        {
            avatar_url = avatarUrl,
            displayname = displayName,
        });
    }

    [Route("{userId}/avatar_url")]
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvatarUrl(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }
        if (!CheckUserId(userId))
        {
            return UserNotFound(userId);
        }

        string? avatarUrl = await userProfile.GetAvatarUrlAsync(userId);
        return new JsonResult(new { avatar_url = avatarUrl });
    }

    [Route("{userId}/avatar_url")]
    [HttpPut]
    public async Task<IActionResult> SetAvatarUrl(
        [FromRoute] string userId,
        [FromBody] SetAvatarUrlRequest requestBody)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }
        if (!CheckUserId(userId))
        {
            return UserNotFound(userId);
        }

        await userProfile.SetAvatarUrlAsync(userId, requestBody.AvatarUrl);
        notifier.Notify(new(ProfileUpdateType.AvatarUrl, requestBody.AvatarUrl));
        return new JsonResult(new object());
    }

    [Route("{userId}/displayname")]
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetDisplayName(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }
        if (!CheckUserId(userId))
        {
            return UserNotFound(userId);
        }

        string? displayName = await userProfile.GetDisplayNameAsync(userId);
        return new JsonResult(new { displayname = displayName });
    }

    [Route("{userId}/displayname")]
    [HttpPut]
    public async Task<IActionResult> SetDisplayName(
        [FromRoute] string userId,
        [FromBody] SetDisplayNameRequest requestBody)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }
        if (!CheckUserId(userId))
        {
            return UserNotFound(userId);
        }

        await userProfile.SetDisplayNameAsync(userId, requestBody.DisplayName);
        notifier.Notify(new(ProfileUpdateType.DisplayName, requestBody.DisplayName));
        return new JsonResult(new object());
    }
}
