using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.P2p.Libp2p.Notifiers;
using MessageHub.HomeServer.Services;
using Microsoft.Net.Http.Headers;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.PubSub;

internal class RequestForwardService : QueuedService<RemoteRequest>
{
    private readonly PubSubServiceContext context;
    private readonly ILogger logger;

    public RequestForwardService(PubSubServiceContext context)
        : base(context.RemoteRequestNotifier, boundedCapacity: 16, maxDegreeOfParallelism: 3)
    {
        this.context = context;
        logger = context.LoggerFactory.CreateLogger<RequestForwardService>();
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running request forward service.");
    }

    protected override async Task RunAsync(RemoteRequest value, CancellationToken stoppingToken)
    {
        var (topic, message) = value;
        logger.LogDebug("Received event for topic {}", topic);
        try
        {
            if (!context.Rooms.HasRoom(topic))
            {
                logger.LogDebug("Room {} not found", topic);
                return;
            }
            var signedRequest = message.Deserialize<SignedRequest>();
            if (signedRequest is null)
            {
                logger.LogDebug("Deserialized event is null");
                return;
            }
            if (signedRequest.Origin == context.IdentityService.GetSelfIdentity().Id)
            {
                return;
            }
            var uri = new Uri($"{context.SelfUrl}{signedRequest.Uri}");
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
            using var client = context.HttpClientFactory.CreateClient();
            var response = await client.SendAsync(request, stoppingToken);
            try
            {
                var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>(
                    cancellationToken: stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogDebug("Response from {}: {}", uri, responseBody);
                }
                else
                {
                    logger.LogDebug("Error response from {}: {}", uri, responseBody);
                }
            }
            catch (Exception)
            {
                logger.LogDebug("Error response from {}: {}", uri, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error processing event for topic {}: {}", topic, message);
        }
    }
}
