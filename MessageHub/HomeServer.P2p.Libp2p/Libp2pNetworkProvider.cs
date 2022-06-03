using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.P2p.Libp2p.Services;
using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.P2p.Providers;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal sealed class Libp2pNetworkProvider : IDisposable, INetworkProvider
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly DHTConfig dhtConfig;
    private readonly Host host;
    private readonly AddressCache addressCache;
    private readonly PublishEventNotifier publishEventNotifier;
    private readonly IP2pService[] p2pServices;
    private P2pNode? p2pNode;
    private BackgroundService? backgroundService;

    public Libp2pNetworkProvider(
        ILoggerFactory loggerFactory,
        HostConfig hostConfig,
        DHTConfig dhtConfig,
        AddressCache addressCache,
        PublishEventNotifier publishEventNotifier,
        HttpProxyService httpProxyService,
        MdnsBackgroundService mdnsBackgroundService,
        DiscoveryService discoveryService,
        PubSubService pubsubService,
        MembershipService membershipService,
        BackfillingService backfillingService)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(hostConfig);
        ArgumentNullException.ThrowIfNull(dhtConfig);
        ArgumentNullException.ThrowIfNull(addressCache);
        ArgumentNullException.ThrowIfNull(publishEventNotifier);
        ArgumentNullException.ThrowIfNull(httpProxyService);
        ArgumentNullException.ThrowIfNull(mdnsBackgroundService);
        ArgumentNullException.ThrowIfNull(discoveryService);
        ArgumentNullException.ThrowIfNull(pubsubService);
        ArgumentNullException.ThrowIfNull(membershipService);
        ArgumentNullException.ThrowIfNull(backfillingService);

        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger<Libp2pNetworkProvider>();
        this.dhtConfig = dhtConfig;
        host = Host.Create(hostConfig);
        this.addressCache = addressCache;
        this.publishEventNotifier = publishEventNotifier;
        p2pServices = new IP2pService[]
        {
            httpProxyService,
            mdnsBackgroundService,
            discoveryService,
            pubsubService,
            membershipService,
            backfillingService
        };
    }

    public (KeyIdentifier, string) GetVerifyKey()
    {
        return (AuthenticatedPeer.KeyIdentifier, host.Id);
    }

    public void Dispose()
    {
        host.Dispose();
    }

    public void Initialize(Func<ServerKeys, IIdentity?> identityVerifier)
    {
        logger.LogInformation("Initializing Libp2p...");
        logger.LogInformation("Host ID: {}", host.Id);
        if (p2pNode is not null)
        {
            logger.LogInformation("Already initialized.");
            return;
        }

        var dht = DHT.Create(host, dhtConfig);
        dht.Bootstrap();
        var discovery = Discovery.Create(dht);
        var memberStore = new MemberStore();
        var pubsub = PubSub.Create(dht, memberStore);

        p2pNode = new P2pNode(
            host,
            dht,
            discovery,
            memberStore,
            pubsub,
            loggerFactory,
            identityVerifier,
            addressCache);
        backgroundService = BackgroundService.Aggregate(p2pServices.Select(x => x.Create(p2pNode)).ToArray());
        backgroundService.Start();
    }

    public void Shutdown()
    {
        if (p2pNode is not null)
        {
            backgroundService?.Stop();
            backgroundService = null;
            p2pNode.Dispose();
            p2pNode = null;
        }
    }

    public void Publish(string roomId, JsonElement message)
    {
        publishEventNotifier.Notify(new(roomId, message));
    }

    public Task<JsonElement> SendAsync(SignedRequest request, CancellationToken cancellationToken)
    {
        if (p2pNode is null)
        {
            throw new InvalidOperationException();
        }
        return p2pNode.SendAsync(request, cancellationToken);
    }

    public Task<Stream> DownloadAsync(string peerId, string url)
    {
        throw new NotImplementedException();
    }

    public Task<IIdentity[]> SearchPeersAsync(
        IIdentity selfIdentity,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (p2pNode is null)
        {
            throw new InvalidOperationException();
        }
        return p2pNode.SearchPeersAsync(selfIdentity, searchTerm, cancellationToken);
    }
}
