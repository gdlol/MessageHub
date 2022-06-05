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
    Task DownloadAsync(string id, string url, string filePath, CancellationToken cancellationToken);
    Task<IEnumerable<IIdentity>> SearchPeersAsync(
        IIdentity selfIdentity,
        string searchTerm,
        CancellationToken cancellationToken = default);
}
