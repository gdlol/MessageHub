using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    public class PushMessagesRequest
    {
        [JsonPropertyName("edus")]
        public EphemeralDataUnit[]? Edus { get; set; }

        [Required]
        [JsonPropertyName("origin")]
        public string Origin { get; set; } = default!;

        [Required]
        [JsonPropertyName("origin_server_ts")]
        public long OriginServerTimestamp { get; set; }

        [Required]
        [JsonPropertyName("pdus")]
        public PersistentDataUnit[] Pdus { get; set; } = default!;
    }

    private readonly IPeerIdentity identity;
    private readonly IEventReceiver eventReceiver;

    public TransactionsController(IPeerIdentity identity, IEventReceiver eventReceiver)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(eventReceiver);

        this.identity = identity;
        this.eventReceiver = eventReceiver;
    }

    [Route("send/{txnId}")]
    [HttpPut]
    public async Task<JsonElement> PushMessages(
        [FromRoute(Name = "txnId")] string _,
        [FromBody] PushMessagesRequest requestBody)
    {
        SignedRequest request = (SignedRequest)Request.HttpContext.Items[nameof(request)]!;
        var errors = await eventReceiver.SendPersistentEventsAsync(requestBody.Pdus);
        if (requestBody.Edus is not null)
        {
            await eventReceiver.SendEphemeralEventsAsync(requestBody.Edus);
        }
        var response = new
        {
            pdus = errors.ToDictionary(x => x.Key, x =>
            {
                string? error = x.Value;
                return error is null ? new object() : new { error };
            })
        };
        return identity.SignResponse(request, response);
    }
}
