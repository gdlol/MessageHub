using System.Text.Json;
using MessageHub.Federation.Protocol;

namespace MessageHub.HomeServer.P2p.Providers;

public interface INetworkProvider
{
    Task InitializeAsync(Func<ServerKey, IPeerIdentity?> identityVerifier);
    void Publish(string roomId, JsonElement message);
    void Subscribe(Action<string, JsonElement> subscriber);
    Task<JsonElement> SendAsync(SignedRequest request, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string peerId, string url);
}
