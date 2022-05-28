using System.Text.Json;
using MessageHub.Federation.Protocol;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal class P2pNode : IDisposable
{
    private readonly Host host;
    private readonly DHT dht;
    private readonly Discovery discovery;
    private readonly MemberStore memberStore;
    private readonly PubSub pubsub;
    private readonly Notifier<object?> shutdownNotifier;
    private readonly ILogger logger;
    private readonly IPeerResolver resolver;
    private readonly Proxy proxy;
    private readonly PubSubService pubsubService;
    private readonly MembershipService membershipService;

    public P2pNode(
        Host host,
        DHT dht,
        Discovery discovery,
        MemberStore memberStore,
        PubSub pubsub,
        Notifier<object?> shutdownNotifier,
        ILogger logger,
        IPeerResolver resolver,
        Proxy proxy,
        PubSubService pubsubService,
        MembershipService membershipService)
    {
        this.host = host;
        this.dht = dht;
        this.discovery = discovery;
        this.memberStore = memberStore;
        this.pubsub = pubsub;
        this.shutdownNotifier = shutdownNotifier;
        this.logger = logger;
        this.resolver = resolver;
        this.proxy = proxy;
        this.pubsubService = pubsubService;
        this.membershipService = membershipService;
    }

    public void Shutdown()
    {
        pubsubService.Stop();
        membershipService.Stop();
        proxy.Stop();
        shutdownNotifier.Notify(null);
    }

    public void Dispose()
    {
        proxy.Dispose();
        pubsub.Dispose();
        memberStore.Dispose();
        discovery.Dispose();
        dht.Dispose();
    }

    public void Publish(string roomId, JsonElement message)
    {
        pubsubService.Publish(roomId, message);
    }

    public async Task<JsonElement> SendAsync(SignedRequest request, CancellationToken cancellationToken)
    {
        string addressInfo = await resolver.ResolveAddressInfoAsync(
            request.Destination,
            cancellationToken: cancellationToken);
        host.Connect(addressInfo, cancellationToken);
        var peerId = Host.GetIdFromAddressInfo(addressInfo);
        var response = host.SendRequest(peerId, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger!.LogInformation("Response status code: {}", response.StatusCode);
        }
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);
        logger!.LogDebug("Response from {} {}: {}", request.Destination, request.Uri, result);
        response.EnsureSuccessStatusCode();
        return result;
    }
}
