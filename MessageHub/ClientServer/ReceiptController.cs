using MessageHub.Authentication;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events.General;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("/_matrix/client/{version}/")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class ReceiptController : ControllerBase
{
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;
    private readonly TimelineUpdateNotifier notifier;
    private readonly IUserReceipts userReceipts;
    private readonly IEventPublisher eventPublisher;

    public ReceiptController(
        ILogger<ReceiptController> logger,
        IIdentityService identityService,
        IRooms rooms,
        TimelineUpdateNotifier notifier,
        IUserReceipts userReceipts,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(userReceipts);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.logger = logger;
        this.identityService = identityService;
        this.rooms = rooms;
        this.notifier = notifier;
        this.userReceipts = userReceipts;
        this.eventPublisher = eventPublisher;
    }

    [Route("rooms/{roomId}/receipt/{receiptType}/{eventId}")]
    [HttpPost]
    public async Task<object> SendReceipt(string roomId, string receiptType, string eventId)
    {
        if (!rooms.HasRoom(roomId))
        {
            logger.LogWarning("Room not found: {}", roomId);
            return new object();
        }
        if (receiptType != ReceiptTypes.Read)
        {
            logger.LogWarning("Unknown receipt type: {}", receiptType);
            return new object();
        }

        var identity = identityService.GetSelfIdentity();
        var sender = UserIdentifier.FromId(identity.Id);
        var userId = sender.ToString();

        var receipt = ReceiptEvent.Create(userId, roomId, eventId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await userReceipts.PutReceiptAsync(
            roomId,
            userId,
            ReceiptTypes.Read,
            receipt.Content[roomId].ReadReceipts[userId]);
        var edu = receipt.ToEdu();
        await eventPublisher.PublishAsync(roomId, edu);

        notifier.Notify();
        return new object();
    }
}
