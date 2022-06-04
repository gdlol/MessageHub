using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MessageHub.Authentication;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Notifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}/profile")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class UserProfileController : ControllerBase
{
    public class SetAvatarUrlRequest
    {
        [Required]
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; } = default!;
    }

    public class SetDisplayNameRequest
    {
        [Required]
        [JsonPropertyName("displayname")]
        public string DisplayName { get; set; } = default!;
    }

    private readonly IUserProfile userProfile;
    private readonly UserProfileUpdateNotifier notifier;

    public UserProfileController(IUserProfile userProfile, UserProfileUpdateNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(userProfile);
        ArgumentNullException.ThrowIfNull(notifier);

        this.userProfile = userProfile;
        this.notifier = notifier;
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

        await userProfile.SetDisplayNameAsync(userId, requestBody.DisplayName);
        notifier.Notify(new(ProfileUpdateType.DisplayName, requestBody.DisplayName));
        return new JsonResult(new object());
    }
}
