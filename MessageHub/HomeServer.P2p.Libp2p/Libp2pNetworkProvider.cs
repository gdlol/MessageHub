using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.Extensions.Caching.Memory;

namespace MessageHub.HomeServer.P2p.Libp2p;

public sealed class Libp2pNetworkProvider : IDisposable, INetworkProvider
{
    private readonly DHTConfig dhtConfig;
    private readonly Host host;
    private P2pNode? p2pNode;

    public Libp2pNetworkProvider(HostConfig hostConfig, DHTConfig dhtConfig)
    {
        ArgumentNullException.ThrowIfNull(hostConfig);
        ArgumentNullException.ThrowIfNull(dhtConfig);

        this.dhtConfig = dhtConfig;
        host = Host.Create(hostConfig);
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

    public async Task InitializeAsync(
        IPeerIdentity identity,
        IUserProfile userProfile,
        ILoggerFactory loggerFactory,
        IMemoryCache memoryCache,
        Func<ServerKeys, IPeerIdentity?> identityVerifier,
        Action<string, JsonElement> subscriber,
        Notifier<(string, string[])> membershipUpdateNotifier,
        ITimelineLoader timelineLoader,
        IRooms rooms,
        string selfAddress)
    {
        var logger = loggerFactory.CreateLogger<Libp2pNetworkProvider>();
        logger.LogInformation("Initializing Libp2p...");
        logger.LogInformation("Host ID: {}", host.Id);

        var dht = DHT.Create(host, dhtConfig);
        var discovery = Discovery.Create(dht);
        var memberStore = new MemberStore();
        var pubsub = PubSub.Create(dht, memberStore);
        var shutdownNotifier = new Notifier<object?>();
        dht.Bootstrap();
        var resolver = new PeerResolver(loggerFactory, host, discovery, identityVerifier, memoryCache);
        var pubsubService = new PubSubService(identity, membershipUpdateNotifier, pubsub, subscriber, loggerFactory);
        var membershipService = new MembershipService(
            loggerFactory,
            identity,
            memberStore,
            resolver,
            membershipUpdateNotifier);
        logger.LogInformation("Start proxy to self address: {}", selfAddress);
        var proxy = host.StartProxyRequests(selfAddress);
        pubsubService.Start();
        membershipService.Start();

        p2pNode = new P2pNode(
            host,
            dht,
            discovery,
            memberStore,
            pubsub,
            shutdownNotifier,
            logger,
            resolver,
            proxy,
            pubsubService,
            membershipService,
            identityVerifier,
            memoryCache);

        // Update room memberships.
        var batchStates = await timelineLoader.LoadBatchStatesAsync(_ => true, includeLeave: false);
        foreach (var roomId in batchStates.JoinedRoomIds)
        {
            var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
            var members = new List<string>();
            foreach (var (roomStateKey, content) in snapshot.StateContents)
            {
                if (roomStateKey.EventType != EventTypes.Member)
                {
                    continue;
                }
                var memberEvent = JsonSerializer.Deserialize<MemberEvent>(content)!;
                if (memberEvent.MemberShip == MembershipStates.Join)
                {
                    var userIdentifier = UserIdentifier.Parse(roomStateKey.StateKey);
                    members.Add(userIdentifier.PeerId);
                }
            }
            membershipUpdateNotifier.Notify((roomId, members.ToArray()));
        }

        // Advertise self.
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            using var cts = new CancellationTokenSource();
            void cancel(object? sender, object? e) => cts.Cancel();
            shutdownNotifier.OnNotify += cancel;
            var logger = loggerFactory.CreateLogger<Libp2pNetworkProvider>();
            while (true)
            {
                try
                {
                    cts.Token.ThrowIfCancellationRequested();
                    logger.LogDebug("Advertising ID: {}", identity.Id);
                    discovery.Advertise(identity.Id, cts.Token);
                    string hostId = host.Id;
                    string userId = UserIdentifier.FromId(identity.Id).ToString();
                    string displayName = await userProfile.GetDisplayNameAsync(userId) ?? identity.Id;
                    for (int i = 7; i < hostId.Length; i++)
                    {
                        string rendezvousPoint = $"/{displayName}/{hostId[..i]}";
                        if (i == 7)
                        {
                            logger.LogDebug("Advertising rendezvousPoints: {}...", rendezvousPoint);
                        }
                        discovery.Advertise(rendezvousPoint, cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    if (cts.IsCancellationRequested)
                    {
                        shutdownNotifier.OnNotify -= cancel;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error advertising ID: {}", identity.Id);
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        });
    }

    public Task ShutdownAsync()
    {
        if (p2pNode is not null)
        {
            p2pNode.Shutdown();
            p2pNode.Dispose();
            p2pNode = null;
        }
        return Task.CompletedTask;
    }

    public void Publish(string roomId, JsonElement message)
    {
        if (p2pNode is null)
        {
            throw new InvalidOperationException();
        }
        p2pNode.Publish(roomId, message);
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

    public Task<IPeerIdentity[]> SearchPeersAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (p2pNode is null)
        {
            throw new InvalidOperationException();
        }
        return p2pNode.SearchPeersAsync(searchTerm, cancellationToken);
    }
}
