using System.Text.Json;
using MessageHub.Authentication;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class TransactionsController : ControllerBase
{
    private readonly IEventReceiver eventReceiver;

    public TransactionsController(IEventReceiver eventReceiver)
    {
        ArgumentNullException.ThrowIfNull(eventReceiver);

        this.eventReceiver = eventReceiver;
    }

    [Route("send/{txnId}")]
    [HttpPut]
    public async Task<JsonElement> PushMessages(
        [FromRoute(Name = "txnId")] string _,
        [FromBody] PushMessagesRequest requestBody)
    {
        SignedRequest request = (SignedRequest)Request.HttpContext.Items[nameof(request)]!;
        var errors = await eventReceiver.ReceivePersistentEventsAsync(requestBody.Pdus);
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
