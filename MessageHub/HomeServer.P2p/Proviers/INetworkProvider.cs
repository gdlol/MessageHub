using System.Text.Json;
using MessageHub.Federation.Protocol;

namespace MessageHub.HomeServer.P2p.Providers;

public interface INetworkProvider
{
    (KeyIdentifier, string) GetVerifyKey();
    Task InitializeAsync(
        ILoggerFactory loggerFactory,
        Func<ServerKeys, IPeerIdentity?> identityVerifier,
        Action<string, JsonElement> subscriber,
        Notifier<(string, string[])> membershipUpdateNotifier);
    Task ShutdownAsync();
    void Publish(string roomId, JsonElement message);
    Task<JsonElement> SendAsync(SignedRequest request, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string peerId, string url);
}
