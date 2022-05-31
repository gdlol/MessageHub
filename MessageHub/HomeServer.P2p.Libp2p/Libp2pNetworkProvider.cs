using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.P2p.Providers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal sealed class Libp2pNetworkProvider : IDisposable, INetworkProvider
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly DHTConfig dhtConfig;
    private readonly Host host;
    private readonly AddressCache addressCache;
    private readonly DiscoveryService discoveryService;
    private readonly PubSubService pubsubService;
    private readonly MembershipService membershipService;
    private readonly BackfillingService backfillingService;
    private readonly string selfAddress;
    private P2pNode? p2pNode;

    public Libp2pNetworkProvider(
        ILoggerFactory loggerFactory,
        HostConfig hostConfig,
        DHTConfig dhtConfig,
        AddressCache addressCache,
        DiscoveryService discoveryService,
        PubSubService pubsubService,
        MembershipService membershipService,
        BackfillingService backfillingService,
        IServer server)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(hostConfig);
        ArgumentNullException.ThrowIfNull(dhtConfig);
        ArgumentNullException.ThrowIfNull(addressCache);
        ArgumentNullException.ThrowIfNull(discoveryService);
        ArgumentNullException.ThrowIfNull(pubsubService);
        ArgumentNullException.ThrowIfNull(membershipService);
        ArgumentNullException.ThrowIfNull(backfillingService);
        ArgumentNullException.ThrowIfNull(server);

        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger<Libp2pNetworkProvider>();
        this.dhtConfig = dhtConfig;
        host = Host.Create(hostConfig);
        this.addressCache = addressCache;
        this.discoveryService = discoveryService;
        this.pubsubService = pubsubService;
        this.membershipService = membershipService;
        this.backfillingService = backfillingService;
        string selfUrl = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
        var uri = new Uri(selfUrl);
        selfAddress = $"{uri.Host}:{uri.Port}";
    }

    public (KeyIdentifier, string) GetVerifyKey()
    {
        var identifier = new KeyIdentifier("libp2p", "PeerID");
        return (identifier, host.Id);
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
        var resolver = new PeerResolver(loggerFactory, host, discovery, identityVerifier, addressCache);
        discoveryService.Start(host, discovery);
        pubsubService.Start(pubsub);
        membershipService.Start(memberStore, resolver);
        var mdnsService = MdnsService.Create(host, nameof(MessageHub));
        mdnsService.Start();
        var proxy = host.StartProxyRequests(selfAddress);

        p2pNode = new P2pNode(
            host,
            dht,
            discovery,
            memberStore,
            pubsub,
            logger,
            resolver,
            mdnsService,
            proxy,
            identityVerifier,
            addressCache);
        // backfillingService.Start(memberStore, p2pNode);
    }

    public void Shutdown()
    {
        backfillingService.Stop();
        if (p2pNode is not null)
        {
            p2pNode.Dispose();
            p2pNode = null;
        }
        membershipService.Stop();
        pubsubService.Stop();
        discoveryService.Stop();
    }

    public void Publish(string roomId, JsonElement message)
    {
        pubsubService.Publish(roomId, message);
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

    public Task<IIdentity[]> SearchPeersAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (p2pNode is null)
        {
            throw new InvalidOperationException();
        }
        return p2pNode.SearchPeersAsync(searchTerm, cancellationToken);
    }
}
