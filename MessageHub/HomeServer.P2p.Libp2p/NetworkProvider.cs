using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.Libp2p;

public class NetworkProvider : INetworkProvider
{
    public Task InitializeAsync(Func<ServerKey, IPeerIdentity?> identityVerifier)
    {
        throw new NotImplementedException();
    }

    public void Publish(string roomId, JsonElement message)
    {
        throw new NotImplementedException();
    }

    public void Subscribe(Action<string, JsonElement> subscriber)
    {
        throw new NotImplementedException();
    }

    public Task<JsonElement> SendAsync(SignedRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> DownloadAsync(string peerId, string url)
    {
        throw new NotImplementedException();
    }
}
