using System.Text.Json;
using MessageHub.Authentication;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class TransactionsController : ControllerBase
{
    private ILogger logger;
    private readonly IEventReceiver eventReceiver;
    private readonly IRooms rooms;

    public TransactionsController(ILogger<TransactionsController> logger, IEventReceiver eventReceiver, IRooms rooms)
    {
        ArgumentNullException.ThrowIfNull(eventReceiver);

        this.logger = logger;
        this.eventReceiver = eventReceiver;
        this.rooms = rooms;
    }

    [Route("send/{txnId}")]
    [HttpPut]
    public async Task<JsonElement> PushMessages(
        [FromRoute(Name = "txnId")] string _,
        [FromBody] PushMessagesRequest requestBody)
    {
        SignedRequest request = (SignedRequest)Request.HttpContext.Items[nameof(request)]!;
        var pdus = new List<PersistentDataUnit>();
        foreach (var pdu in requestBody.Pdus)
        {
            if (pdu is null)
            {
                logger.LogInformation("Null pdu in request.");
                continue;
            }
            if (!rooms.HasRoom(pdu.RoomId))
            {
                logger.LogInformation("RoomId not in joined rooms: {}", pdu.RoomId);
                continue;
            }
            pdus.Add(pdu);
        }
        var errors = await eventReceiver.ReceivePersistentEventsAsync(pdus.ToArray());
        if (requestBody.Edus is not null)
        {
            await eventReceiver.ReceiveEphemeralEventsAsync(requestBody.Edus);
        }
        var response = new
        {
            pdus = errors.ToDictionary(x => x.Key, x =>
            {
                string? error = x.Value;
                return error is null ? new object() : new { error };
            })
        };
        return JsonSerializer.SerializeToElement(response);
    }
}
