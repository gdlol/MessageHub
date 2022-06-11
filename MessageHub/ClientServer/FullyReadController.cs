using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Authentication;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events.General;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class FullyReadController : ControllerBase
{
    public class SetReadMarkerRequest
    {
        [Required]
        [JsonPropertyName("m.fully_read")]
        public string FullyRead { get; set; } = default!;

        [JsonPropertyName("m.read")]
        public string? Read { get; set; }
    }

    private readonly TimelineUpdateNotifier notifier;
    private readonly IRooms rooms;
    private readonly IAccountData accountData;

    public FullyReadController(TimelineUpdateNotifier notifier, IRooms rooms, IAccountData accountData)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(accountData);

        this.notifier = notifier;
        this.rooms = rooms;
        this.accountData = accountData;
    }

    [Route("rooms/{roomId}/read_markers")]
    [HttpPost]
    public async Task<IActionResult> SetReadMarker(
        [FromRoute] string roomId,
        [FromBody] SetReadMarkerRequest requestBody)
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
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
            JsonSerializer.SerializeToElement(fullyReadContent));
        notifier.Notify();
        return new JsonResult(new object());
    }
}
