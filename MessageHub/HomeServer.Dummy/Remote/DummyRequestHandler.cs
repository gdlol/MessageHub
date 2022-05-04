using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using Microsoft.Net.Http.Headers;

namespace MessageHub.HomeServer.Dummy.Remote;

public class DummyRequestHandler : IRequestHandler
{
    private readonly ILogger logger;
    private readonly Config config;
    private readonly IRooms rooms;
    private readonly HttpClient client;

    public DummyRequestHandler(
        ILogger<DummyRequestHandler> logger,
        Config config,
        IRooms rooms,
        HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(client);

        this.logger = logger;
        this.config = config;
        this.rooms = rooms;
        this.client = client;
    }

    public async Task<JsonElement> SendRequest(SignedRequest signedRequest)
    {
        var signatures = signedRequest.Signatures.Deserialize<Signatures>()!;
        var senderSignatures = signatures[signedRequest.Origin];
        JsonElement? result = null;
        foreach (var (peerId, host) in config.Peers)
        {
            if (peerId == config.PeerId)
            {
                continue;
            }
            if (rooms.HasRoom(signedRequest.Destination))
            {
                bool isMember = false;
                var snapshot = await rooms.GetRoomSnapshotAsync(signedRequest.Destination);
                var peerUserId = UserIdentifier.FromId(peerId).ToString();
                if (snapshot.StateContents.TryGetValue(
                    new RoomStateKey(EventTypes.Member, peerUserId),
                    out var content))
                {
                    var memberEvent = JsonSerializer.Deserialize<MemberEvent>(content)!;
                    if (memberEvent.MemberShip == MembershipStates.Join)
                    {
                        isMember = true;
                    }
                }
                if (!isMember)
                {
                    continue;
                }
            }
            logger.LogInformation("Sending {signedRequest} to {peerId}", signedRequest, peerId);

            var uri = new Uri($"http://{host}{signedRequest.Uri}");
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
            foreach (var (key, signature) in senderSignatures)
            {
                request.Headers.Add(
                    HeaderNames.Authorization,
                    $"X-Matrix origin={signedRequest.Origin},key=\"{key}\",sig=\"{signature}\"");
            }
            var response = await client.SendAsync(request);
            try
            {
                var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (response.IsSuccessStatusCode)
                {
                    result = responseBody;
                }
                else
                {
                    logger.LogError("Error response from {}: {}", uri, responseBody);
                }
            }
            catch (Exception)
            {
                logger.LogError("Error response from {}: {}", uri, response.ReasonPhrase);
            }
        }
        result ??= JsonSerializer.SerializeToElement<object?>(null);
        return result.Value;
    }

    public Task<Stream> DownloadAsync(string peerId, string url)
    {
        var host = config.Peers[peerId];
        return client.GetStreamAsync($"http://{host}{url}");
    }
}
