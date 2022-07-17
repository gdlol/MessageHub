using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Remote;
using MessageHub.Serialization;

namespace MessageHub.HomeServer.P2p.Remote;

public class RequestHandler : IRequestHandler
{
    private readonly INetworkProvider networkProvider;

    public RequestHandler(INetworkProvider networkProvider)
    {
        ArgumentNullException.ThrowIfNull(networkProvider);

        this.networkProvider = networkProvider;
    }

    public async Task<JsonElement> SendRequest(SignedRequest signedRequest)
    {
        if (RoomIdentifier.TryParse(signedRequest.Destination, out var _))
        {
            networkProvider.Publish(
                signedRequest.Destination,
                DefaultJsonSerializer.SerializeToElement(signedRequest));
            return DefaultJsonSerializer.SerializeToElement<object?>(null);
        }
        else
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(60));
            return await networkProvider.SendAsync(signedRequest, cts.Token);
        }
    }
}
