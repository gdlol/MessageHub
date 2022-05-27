using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.Libp2p;

public sealed class Libp2pNetworkProvider : INetworkProvider, IDisposable
{
    private readonly Host host;
    private readonly DHT dht;
    private readonly Discovery discovery;
    private readonly MemberStore memberStore;
    private readonly PubSub pubsub;
    private PubSubService? pubsubService;
    private MembershipService? membershipService;

    public Libp2pNetworkProvider(HostConfig hostConfig, DHTConfig dhtConfig)
    {
        ArgumentNullException.ThrowIfNull(hostConfig);
        ArgumentNullException.ThrowIfNull(dhtConfig);

        host = Host.Create(hostConfig);
        dht = DHT.Create(host, dhtConfig);
        discovery = Discovery.Create(dht);
        memberStore = new MemberStore();
        pubsub = PubSub.Create(dht, memberStore);
    }

    public (KeyIdentifier, string) GetVerifyKey()
    {
        var identifier = new KeyIdentifier("libp2p", "ID");
        return (identifier, host.Id);
    }

    public void Dispose()
    {
        pubsub.Dispose();
        memberStore.Dispose();
        discovery.Dispose();
        pubsub.Dispose();
        dht.Dispose();
        host.Dispose();
    }

    public Task InitializeAsync(
        ILoggerFactory loggerFactory,
        Func<ServerKeys, IPeerIdentity?> identityVerifier,
        Action<string, JsonElement> subscriber,
        Notifier<(string, string[])> membershipUpdateNotifier)
    {
        dht.Bootstrap();
        var resolver = new PeerResolver(loggerFactory, host, discovery, identityVerifier);
        pubsubService = new PubSubService(pubsub, subscriber, loggerFactory);
        membershipService = new MembershipService(loggerFactory, memberStore, resolver, membershipUpdateNotifier);
        pubsubService.Start();
        membershipService.Start();
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        if (pubsubService is not null)
        {
            pubsubService.Stop();
            pubsubService = null;
        }
        if (membershipService is not null)
        {
            membershipService.Stop();
            membershipService = null;
        }
        return Task.CompletedTask;
    }

    public void Publish(string roomId, JsonElement message)
    {
        if (pubsubService is null)
        {
            throw new InvalidOperationException();
        }
        pubsubService.Publish(roomId, message);
    }

    public async Task<JsonElement> SendAsync(SignedRequest request, CancellationToken cancellationToken)
    {
        IPeerResolver resolver = default!;
        string addressInfo = await resolver.ResolveAddressInfoAsync(
            request.Destination,
            cancellationToken: cancellationToken);
        host.Connect(addressInfo, cancellationToken);
        var response = host.SendRequest(request.Destination, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);
        return result;
    }

    public Task<Stream> DownloadAsync(string peerId, string url)
    {
        throw new NotImplementedException();
    }
}
