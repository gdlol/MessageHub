using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class AuthorizationEventsController : ControllerBase
{
    private readonly IEventStore eventStore;

    public AuthorizationEventsController(IEventStore eventStore)
    {
        ArgumentNullException.ThrowIfNull(eventStore);

        this.eventStore = eventStore;
    }

    [Route("{roomId}/{eventId}")]
    [HttpGet]
    public async Task<IActionResult> GetAuthorizationEvents(string roomId, string eventId)
    {
        if (!eventStore.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }
        var roomEventStore = await eventStore.GetRoomEventStoreAsync(roomId);
        var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(new[] { eventId });
        if (missingEventIds.Length > 0)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(eventId)));
        }
        var pdu = await roomEventStore.LoadEventAsync(eventId);
        var authChain = new List<PersistentDataUnit>();
        foreach (string authEventId in pdu.AuthorizationEvents)
        {
            pdu = await roomEventStore.LoadEventAsync(authEventId);
            authChain.Add(pdu);
        }
        return new JsonResult(new
        {
            auth_chain = authChain.ToArray()
        });
    }
}
