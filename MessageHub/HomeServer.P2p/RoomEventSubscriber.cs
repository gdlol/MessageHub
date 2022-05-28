using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.P2p.Rooms;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Net.Http.Headers;

namespace MessageHub.HomeServer.P2p;

internal class RoomEventSubscriber
{
    private readonly ILogger logger;
    private readonly EventStore eventStore;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly string selfUrl;

    public RoomEventSubscriber(
        ILogger<RoomEventSubscriber> logger,
        EventStore eventStore,
        IHttpClientFactory httpClientFactory,
        IServer server)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(server);

        this.logger = logger;
        this.eventStore = eventStore;
        this.httpClientFactory = httpClientFactory;
        selfUrl = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    public void ReceiveEvent(string topic, JsonElement element)
    {
        IPeerIdentity peerIdentity = DummyIdentity.Self ?? throw new InvalidOperationException();
        logger.LogDebug("Received event for topic {}", topic);
        Task.Run(async () =>
        {
            try
            {
                if (!eventStore.Update().JoinedRoomIds.Contains(topic))
                {
                    logger.LogDebug("Room {} not found", topic);
                    return;
                }
                var signedRequest = element.Deserialize<SignedRequest>();
                if (signedRequest is null)
                {
                    logger.LogDebug("Deserialized event is null");
                    return;
                }
                if (signedRequest.Origin == peerIdentity.Id)
                {
                    return;
                }
                var uri = new Uri($"{selfUrl}{signedRequest.Uri}");
                var request = new HttpRequestMessage
                {
                    Method = new HttpMethod(signedRequest.Method),
                    RequestUri = uri,
                };
                if (signedRequest.Content is not null)
                {
                    request.Content = JsonContent.Create(signedRequest.Content);
                }
                request.Headers.Add("Matrix-Host", signedRequest.Destination);
                request.Headers.Add("Matrix-Timestamp", signedRequest.OriginServerTimestamp.ToString());
                request.Headers.Add(
                    "Matrix-ServerKeys",
                    Convert.ToHexString(JsonSerializer.SerializeToUtf8Bytes(signedRequest.ServerKeys)));
                var signatures = signedRequest.Signatures.Deserialize<Signatures>()!;
                var senderSignatures = signatures[signedRequest.Origin];
                var authorizationHeaders = new List<string>();
                foreach (var (key, signature) in senderSignatures)
                {
                    authorizationHeaders.Add(
                        $"X-Matrix origin={signedRequest.Origin},key=\"{key}\",sig=\"{signature}\"");
                }
                request.Headers.Add(HeaderNames.Authorization, authorizationHeaders);
                using var client = httpClientFactory.CreateClient();
                var response = await client.SendAsync(request);
                try
                {
                    var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (response.IsSuccessStatusCode)
                    {
                        logger.LogDebug("Response from {}: {}", uri, responseBody);
                    }
                    else
                    {
                        logger.LogWarning("Error response from {}: {}", uri, responseBody);
                    }
                }
                catch (Exception)
                {
                    logger.LogWarning("Error response from {}: {}", uri, response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing event for topic {}: {}", topic, element);
            }
        });
    }
}
