using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Providers;

public interface INetworkProvider
{
    (KeyIdentifier, string) GetVerifyKey();
    Task InitializeAsync(
        IPeerIdentity identity,
        ILoggerFactory loggerFactory,
        Func<ServerKeys, IPeerIdentity?> identityVerifier,
        Action<string, JsonElement> subscriber,
        Notifier<(string, string[])> membershipUpdateNotifier,
        ITimelineLoader timelineLoader,
        IRooms rooms,
        string selfAddress);
    Task ShutdownAsync();
    void Publish(string roomId, JsonElement message);
    Task<JsonElement> SendAsync(SignedRequest request, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string peerId, string url);
}
