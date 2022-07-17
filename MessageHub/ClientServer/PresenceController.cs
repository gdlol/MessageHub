using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Notifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("/_matrix/client/{version}/")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class PresenceController : ControllerBase
{
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IUserPresence userPresence;
    private readonly PresenceUpdateNotifier notifier;

    public PresenceController(
        ILogger<PresenceController> logger,
        IIdentityService identityService,
        IUserPresence userPresence,
        PresenceUpdateNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(userPresence);
        ArgumentNullException.ThrowIfNull(notifier);

        this.logger = logger;
        this.identityService = identityService;
        this.userPresence = userPresence;
        this.notifier = notifier;
    }

    [Route("presence/{userId}/status")]
    [HttpGet]
    public IActionResult GetStatus(string userId)
    {
        var presence = userPresence.GetPresence(userId);
        if (presence is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        return new JsonResult(new GetPresenceResponse
        {
            CurrentlyActive = presence.CurrentlyActive,
            LastActiveAgo = presence.LastActiveAgo,
            Presence = presence.Presence,
            StatusMessage = presence.StatusMessage
        });
    }

    [Route("presence/{userId}/status")]
    [HttpPut]
    public object SetStatus(string userId, SetPresenceRequest request)
    {
        var identity = identityService.GetSelfIdentity();
        if (UserIdentifier.FromId(identity.Id).ToString() != userId)
        {
            logger.LogWarning("Attempt to set presence status of userId: {}", userId);
        }
        else
        {
            userPresence.SetPresence(userId, request.Presence, request.StatusMessage);
            notifier.Notify();
        }
        return new object();
    }
}
