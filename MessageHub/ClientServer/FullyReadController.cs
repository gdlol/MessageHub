using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.ClientServer.Protocol;
using MessageHub.ClientServer.Protocol.Events;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
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

    private readonly IRoomLoader roomLoader;
    private readonly IAccountData accountData;
    private readonly IEventSender eventSender;

    public FullyReadController(
        IRoomLoader roomLoader,
        IAccountData accountData,
        IEventSender eventSender)
    {
        ArgumentNullException.ThrowIfNull(roomLoader);
        ArgumentNullException.ThrowIfNull(accountData);
        ArgumentNullException.ThrowIfNull(eventSender);

        this.roomLoader = roomLoader;
        this.accountData = accountData;
        this.eventSender = eventSender;
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
        if (!roomLoader.HasRoom(roomId))
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
        if (requestBody.Read is not null)
        {
            _ = eventSender;
        }
        return new JsonResult(new object());
    }
}
