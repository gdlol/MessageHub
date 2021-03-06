using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events.General;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class FullyReadController : ControllerBase
{
    private readonly TimelineUpdateNotifier notifier;
    private readonly IRooms rooms;
    private readonly IAccountData accountData;
    private readonly IUserReceipts userReceipts;
    private readonly IEventPublisher eventPublisher;

    public FullyReadController(
        TimelineUpdateNotifier notifier,
        IRooms rooms,
        IAccountData accountData,
        IUserReceipts userReceipts,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(accountData);
        ArgumentNullException.ThrowIfNull(userReceipts);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.notifier = notifier;
        this.rooms = rooms;
        this.accountData = accountData;
        this.userReceipts = userReceipts;
        this.eventPublisher = eventPublisher;
    }

    [Route("rooms/{roomId}/read_markers")]
    [HttpPost]
    public async Task<IActionResult> SetReadMarker(
        [FromRoute] string roomId,
        [FromBody] SetReadMarkerRequest requestBody)
    {
        string? userId = HttpContext.User.Identity?.Name ?? throw new InvalidOperationException();
        if (!rooms.HasRoom(roomId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var fullyReadContent = new FullyReadEvent
        {
            EventId = requestBody.FullyRead
        };
        await accountData.SaveAccountDataAsync(
            roomId,
            FullyReadEvent.EventType,
            DefaultJsonSerializer.SerializeToElement(fullyReadContent));

        // Send read receipt.
        if (requestBody.Read is not null)
        {
            var receipt = ReceiptEvent.Create(
                userId,
                roomId,
                requestBody.Read,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await userReceipts.PutReceiptAsync(
                roomId,
                userId,
                ReceiptTypes.Read,
                receipt.Content[roomId].ReadReceipts[userId]);
            var edu = receipt.ToEdu();
            await eventPublisher.PublishAsync(roomId, edu);
        }

        notifier.Notify();

        return new JsonResult(new object());
    }
}
