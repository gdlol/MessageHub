using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;
using MessageHub.HomeServer.Rooms.Timeline;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using Microsoft.AspNetCore.Authorization;
using MessageHub.Authentication;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}/rooms")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class SendMessagesController : ControllerBase
{
    private static readonly JsonSerializerOptions ignoreNullOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IPeerIdentity peerIdentity;
    private readonly IRooms rooms;
    private readonly IEventSaver eventSaver;
    private readonly IEventPublisher eventPublisher;

    public SendMessagesController(
        IPeerIdentity peerIdentity,
        IRooms rooms,
        IEventSaver eventSaver,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.peerIdentity = peerIdentity;
        this.rooms = rooms;
        this.eventSaver = eventSaver;
        this.eventPublisher = eventPublisher;
    }

    [Route("{roomId}/state/{eventType}/{stateKey?}")]
    [HttpPut]
    public async Task<IActionResult> SendStateEvent(
        [FromRoute] string roomId,
        [FromRoute] string eventType,
        [FromRoute] string? stateKey,
        [FromBody] JsonElement body)
    {
        stateKey ??= string.Empty;
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        var senderId = UserIdentifier.Parse(userId);
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }

        var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var authorizer = new EventAuthorizer(snapshot.StateContents);
        if (!authorizer.Authorize(eventType, stateKey, senderId, body))
        {
            return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        (snapshot, var pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: snapshot,
            eventType: eventType,
            stateKey: stateKey,
            sender: senderId,
            content: body,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        string eventId = EventHash.GetEventId(pdu);
        var signedPdu = peerIdentity.SignEvent(pdu);
        await eventSaver.SaveAsync(roomId, eventId, signedPdu, snapshot.States);
        await eventPublisher.PublishAsync(signedPdu);
        return new JsonResult(new { event_id = eventId });
    }

    [Route("{roomId}/send/{eventType}/{txnId}")]
    [HttpPut]
    public async Task<IActionResult> SendMessageEvent(
        [FromRoute] string roomId,
        [FromRoute] string eventType,
        [FromRoute(Name = "txnId")] string transactionId,
        [FromBody] JsonElement body)
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        var senderId = UserIdentifier.Parse(userId);
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }

        var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var authorizer = new EventAuthorizer(snapshot.StateContents);
        if (!authorizer.Authorize(eventType, null, senderId, body))
        {
            return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        (snapshot, var pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: snapshot,
            eventType: eventType,
            stateKey: null,
            sender: senderId,
            content: body,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            unsigned: JsonSerializer.SerializeToElement(
                new UnsignedData
                {
                    TransactionId = transactionId
                },
                ignoreNullOptions));
        string eventId = EventHash.GetEventId(pdu);
        var signedPdu = peerIdentity.SignEvent(pdu);
        await eventSaver.SaveAsync(roomId, eventId, signedPdu, snapshot.States);
        await eventPublisher.PublishAsync(signedPdu);
        return new JsonResult(new { event_id = eventId });
    }

    [Route("{roomId}/redact/{eventId}/{txnId}")]
    [HttpPut]
    public async Task<IActionResult> Redact(
        [FromRoute] string roomId,
        [FromRoute] string eventId,
        [FromRoute(Name = "txnId")] string transactionId,
        [FromBody] JsonElement body)
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        var senderId = UserIdentifier.Parse(userId);
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }

        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
        var redactedEvent = await roomEventStore.TryLoadEventAsync(eventId);
        if (redactedEvent is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(eventId)}: {eventId}"));
        }
        var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var authorizer = new EventAuthorizer(snapshot.StateContents);
        if (!authorizer.Authorize(EventTypes.Redact, null, senderId, body))
        {
            return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        if (redactedEvent.Sender != userId)
        {
            int powerLevel = authorizer.GetPowerLevel(senderId);
            int redactPowerLevel = authorizer.GetPowerLevelsEventOrDefault().Redact;
            if (powerLevel < redactPowerLevel)
            {
                return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
            }
        }
        (snapshot, var pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: snapshot,
            eventType: EventTypes.Redact,
            stateKey: null,
            sender: senderId,
            content: body,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            redacts: eventId,
            unsigned: JsonSerializer.SerializeToElement(
                new UnsignedData
                {
                    TransactionId = transactionId
                },
                ignoreNullOptions));
        string redactEventId = EventHash.GetEventId(pdu);
        var signedPdu = peerIdentity.SignEvent(pdu);
        await eventSaver.SaveAsync(roomId, redactEventId, signedPdu, snapshot.States);
        await eventPublisher.PublishAsync(signedPdu);
        return new JsonResult(new { event_id = redactEventId });
    }
}
