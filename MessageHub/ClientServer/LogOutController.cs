using MessageHub.Authentication;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events.General;
using MessageHub.HomeServer.Notifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class LogOutController : ControllerBase
{
    private readonly IIdentityService identityService;
    private readonly IAuthenticator authenticator;
    private readonly IUserPresence userPresence;
    private readonly PresenceUpdateNotifier presenceUpdateNotifier;

    public LogOutController(
        IIdentityService identityService,
        IAuthenticator authenticator,
        IUserPresence userPresence,
        PresenceUpdateNotifier presenceUpdateNotifier)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(userPresence);
        ArgumentNullException.ThrowIfNull(presenceUpdateNotifier);

        this.identityService = identityService;
        this.authenticator = authenticator;
        this.userPresence = userPresence;
        this.presenceUpdateNotifier = presenceUpdateNotifier;
    }

    private void NotifyUnavailableStatus()
    {
        var identity = identityService.GetSelfIdentity();
        var userId = UserIdentifier.FromId(identity.Id).ToString();
        userPresence.SetPresence(userId, PresenceValues.Unavailable, null);
        presenceUpdateNotifier.Notify();
    }

    [Route("logout")]
    [HttpPost]
    public async Task<object> LogOut()
    {
        if (Request.HttpContext.Items["token"] is string token)
        {
            string? deviceId = await authenticator.GetDeviceIdAsync(token);
            if (deviceId is not null)
            {
                int remainingTokenCount = await authenticator.LogOutAsync(deviceId);
                if (remainingTokenCount == 0)
                {
                    NotifyUnavailableStatus();
                }
            }
        }
        else
        {
            return MatrixError.Create(MatrixErrorCode.NotFound, nameof(token));
        }
        return new object();
    }

    [Route("logout/all")]
    [HttpPost]
    public async Task<object> LogOutAll()
    {
        await authenticator.LogOutAllAsync();
        NotifyUnavailableStatus();
        return new object();
    }
}
