using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MessageHub.ClientServerProtocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}/profile")]
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

    public UserProfileController(IUserProfile userProfile)
    {
        ArgumentNullException.ThrowIfNull(userProfile);

        this.userProfile = userProfile;
    }

    [Route("{userId}")]
    [HttpGet]
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

    [Route("{userId}")]
    [HttpGet]
    public async Task<IActionResult> GetAvatarUrl(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }

        string? avatarUrl = await userProfile.GetAvatarUrlAsync(userId);
        return new JsonResult(new { avatar_url = avatarUrl });
    }

    [Route("{userId}")]
    [HttpPost]
    public async Task<IActionResult> SetAvatarUrl(
        [FromRoute] string userId,
        [FromBody] SetAvatarUrlRequest requestBody)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }

        await userProfile.SetAvatarUrlAsync(userId, requestBody.AvatarUrl);
        return new JsonResult(new object());
    }

    [Route("{userId}")]
    [HttpGet]
    public async Task<IActionResult> GetDisplayName(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }

        string? displayName = await userProfile.GetDisplayNameAsync(userId);
        return new JsonResult(new { displayname = displayName });
    }

    [Route("{userId}")]
    [HttpPost]
    public async Task<IActionResult> SetDisplayName(
        [FromRoute] string userId,
        [FromBody] SetDisplayNameRequest requestBody)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }

        await userProfile.SetDisplayNameAsync(userId, requestBody.DisplayName);
        return new JsonResult(new object());
    }
}
