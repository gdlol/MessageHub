using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.P2p.Libp2p.Notifiers;
using MessageHub.HomeServer.P2p.Libp2p.Services;
using MessageHub.HomeServer.P2p.Providers;
using Microsoft.Extensions.Caching.Memory;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal sealed class Libp2pNetworkProvider : IDisposable, INetworkProvider
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly DHTConfig dhtConfig;
    private readonly Host host;
    private readonly IMemoryCache memoryCache;
    private readonly PublishEventNotifier publishEventNotifier;
    private readonly IP2pService[] p2pServices;
    private P2pNode? p2pNode;
    private BackgroundService? backgroundService;

    public Libp2pNetworkProvider(
        ILoggerFactory loggerFactory,
        HostConfig hostConfig,
        DHTConfig dhtConfig,
        IMemoryCache memoryCache,
        PublishEventNotifier publishEventNotifier,
        AddressCachingService addressCachingService,
        HttpProxyService httpProxyService,
        MdnsBackgroundService mdnsBackgroundService,
        AdvertisingService advertisingService,
        PubSubService pubsubService,
        MembershipService membershipService,
        BackfillingService backfillingService)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(hostConfig);
        ArgumentNullException.ThrowIfNull(dhtConfig);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(publishEventNotifier);
        ArgumentNullException.ThrowIfNull(addressCachingService);
        ArgumentNullException.ThrowIfNull(httpProxyService);
        ArgumentNullException.ThrowIfNull(mdnsBackgroundService);
        ArgumentNullException.ThrowIfNull(advertisingService);
        ArgumentNullException.ThrowIfNull(pubsubService);
        ArgumentNullException.ThrowIfNull(membershipService);
        ArgumentNullException.ThrowIfNull(backfillingService);

        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger<Libp2pNetworkProvider>();
        this.dhtConfig = dhtConfig;
        host = Host.Create(hostConfig);
        this.memoryCache = memoryCache;
        this.publishEventNotifier = publishEventNotifier;
        p2pServices = new IP2pService[]
        {
            addressCachingService,
            httpProxyService,
            mdnsBackgroundService,
            advertisingService,
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
        logger.LogInformation("Initializing libp2p...");
        logger.LogInformation("Host ID: {}", host.Id);
        if (p2pNode is not null)
        {
            logger.LogInformation("Already initialized.");
            return;
        }

        var dht = DHT.Create(host, dhtConfig);
        logger.LogInformation("Bootstrapping DHT...");
        dht.Bootstrap();
        Task.Run(() =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                int count = host.ConnectToSavedPeers(cts.Token);
                logger.LogInformation("Connected to {} peers.", count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error connecting to saved peers.");
            }
        });
        var discovery = Discovery.Create(dht);
        var memberStore = new MemberStore();
        var pubsub = PubSub.Create(dht, memberStore);

        p2pNode = new P2pNode(
            host: host,
            dht: dht,
            discovery: discovery,
            memberStore: memberStore,
            pubsub: pubsub,
            loggerFactory: loggerFactory,
            identityVerifier: identityVerifier,
            addressCache: memoryCache);

        logger.LogInformation("Starting background services...");
        backgroundService = BackgroundService.Aggregate(p2pServices.Select(x => x.Create(p2pNode)).ToArray());
        backgroundService.Start();

        logger.LogInformation("Initialized libp2p.");
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

    public async Task DownloadAsync(string id, string url, string filePath, CancellationToken cancellationToken)
    {
        if (p2pNode is null)
        {
            throw new InvalidOperationException();
        }
        logger.LogDebug("Downloading {}...", $"libp2p://{id}{url}");
        var (addressInfo, _) = await p2pNode.Resolver.ResolveAddressInfoAsync(id, cancellationToken: cancellationToken);
        string peerId = Host.GetIdFromAddressInfo(addressInfo);
        p2pNode.Host.DownloadFile(peerId, url, filePath, cancellationToken);
    }

    public Task<IEnumerable<IIdentity>> SearchPeersAsync(
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
