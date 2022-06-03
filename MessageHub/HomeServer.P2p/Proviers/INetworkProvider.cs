using System.Text.Json;
using MessageHub.Federation.Protocol;

namespace MessageHub.HomeServer.P2p.Providers;

public interface INetworkProvider
{
    (KeyIdentifier, string) GetVerifyKey();
    void Initialize(Func<ServerKeys, IIdentity?> identityVerifier);
    void Shutdown();
    void Publish(string roomId, JsonElement message);
    Task<JsonElement> SendAsync(SignedRequest request, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string id, string url);
    Task<IIdentity[]> SearchPeersAsync(
        IIdentity selfIdentity,
        string searchTerm,
        CancellationToken cancellationToken = default);
}
