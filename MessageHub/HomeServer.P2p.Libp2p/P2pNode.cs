using System.Collections.Concurrent;
using System.Text.Json;
using MessageHub.Federation.Protocol;
using Microsoft.Extensions.Caching.Memory;

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
    private readonly Func<ServerKeys, IPeerIdentity?> identityVerifier;
    private readonly IMemoryCache addressCache;

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
        MembershipService membershipService,
        Func<ServerKeys, IPeerIdentity?> identityVerifier,
        IMemoryCache addressCache)
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
        this.identityVerifier = identityVerifier;
        this.addressCache = addressCache;
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
        ArgumentNullException.ThrowIfNull(roomId);

        pubsubService.Publish(roomId, message);
    }

    public async Task<JsonElement> SendAsync(SignedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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

    public async Task<IPeerIdentity?> GetServerIdentityAsync(
        string peerId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting server identity of {}...", peerId);
        string addressInfo = dht.FindPeer(peerId, cancellationToken);
        host.Connect(addressInfo, cancellationToken);
        var response = await host.GetServerKeysAsync(peerId, cancellationToken);
        var serverKeys = response?.Deserialize<ServerKeys>();
        if (serverKeys is null)
        {
            logger.LogDebug("Null server keys response from {}", peerId);
            return null;
        }
        var identity = identityVerifier(serverKeys);
        if (identity is not null)
        {
            addressCache.Set(
                identity.Id,
                addressInfo,
                DateTimeOffset.FromUnixTimeMilliseconds(serverKeys.ValidUntilTimestamp));
        }
        else
        {
            logger.LogDebug("identity verification failed for {}: {}", peerId, response);
        }
        return identity;
    }

    public IEnumerable<IPeerIdentity> GetPeersForTopic(
        string topic,
        Func<IPeerIdentity, bool> peerFilter,
        CancellationToken cancellationToken = default)
    {
        using var queue = new BlockingCollection<IPeerIdentity>();
        using var _ = cancellationToken.Register(() => queue.CompleteAdding());
        Task.Run(async () =>
        {
            var addressInfos = discovery.FindPeers(topic, cancellationToken);
            logger.LogDebug("Found {} candidate peers for topic: {}...", addressInfos.Count, topic);
            try
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = 8
                };
                await Parallel.ForEachAsync(addressInfos, parallelOptions, async (x, token) =>
                {
                    var (peerId, addressInfo) = x;
                    if (peerId == host.Id)
                    {
                        return;
                    }
                    try
                    {
                        token.ThrowIfCancellationRequested();

                        logger.LogDebug("Connecting to {}: {}...", peerId, addressInfo);
                        host.Connect(addressInfo, token);
                        logger.LogDebug("Connected to {}: {}", peerId, addressInfo);

                        var responseBody = await host.GetServerKeysAsync(peerId, token);
                        var serverKeys = responseBody?.Deserialize<ServerKeys>();
                        if (serverKeys is null)
                        {
                            logger.LogDebug("Null server keys response from {}", peerId);
                            return;
                        }
                        logger.LogDebug("Server keys {}: {}", responseBody, peerId);
                        var identity = identityVerifier(serverKeys);
                        if (identity is not null)
                        {
                            if (peerFilter(identity))
                            {
                                logger.LogDebug("Peer found for topic {}: {}", topic, identity.Id);
                                queue.TryAdd(identity);
                                addressCache.Set(
                                    identity.Id,
                                    addressInfo,
                                    DateTimeOffset.FromUnixTimeMilliseconds(serverKeys.ValidUntilTimestamp));
                            }
                            else
                            {
                                logger.LogDebug("identity {} not satisfying filter", identity.Id);
                            }
                        }
                        else
                        {
                            logger.LogDebug("identity verification failed for {}: {}", peerId, responseBody);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        logger.LogDebug(
                            ex,
                            "Error verifying identify of peer {}",
                            (peerId, addressInfo));
                    }
                });
            }
            catch (OperationCanceledException) { }
            finally
            {
                queue.CompleteAdding();
            }
        }, cancellationToken);
        foreach (var identity in queue.GetConsumingEnumerable(cancellationToken))
        {
            yield return identity;
        }
    }

    public async Task<IPeerIdentity[]> SearchPeersAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            return Array.Empty<IPeerIdentity>();
        }

        string p2pPrefix = "/p2p/";
        if (searchTerm.StartsWith(p2pPrefix))
        {
            string peerId = searchTerm[p2pPrefix.Length..];
            if (peerId == host.Id)
            {
                logger.LogInformation("Peer ID is self ID, returning empty response");
                return Array.Empty<IPeerIdentity>();
            }
            logger.LogInformation("Finding peer address for {}...", peerId);
            try
            {
                var identity = await GetServerIdentityAsync(peerId, cancellationToken);
                if (identity is null)
                {
                    logger.LogInformation("Peer not found for {}", searchTerm);
                }
                else
                {
                    return new[] { identity };
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error finding peer address for {}", peerId);
            }
        }
        else
        {
            // check for search term of the form /Display Name/abcdefg
            var parts = searchTerm.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (searchTerm.StartsWith('/') && parts.Length >= 2)
            {
                string peerIdPrefix = parts[^1];
                logger.LogInformation(
                    "Finding peers with name {} and peer ID prefix {}...",
                    searchTerm[..^peerIdPrefix.Length].Trim('/'),
                    peerIdPrefix);
                bool verifyIdentity(IPeerIdentity identity)
                {
                    if (identity.VerifyKeys.Keys.TryGetValue(new KeyIdentifier("libp2p", "PeerID"), out var key))
                    {
                        return key.StartsWith(peerIdPrefix);
                    }
                    return false;
                }
                var peers = GetPeersForTopic(searchTerm, verifyIdentity, cancellationToken).ToArray();
                logger.LogInformation(
                    "Found {} peers with name {} and peer ID prefix {}", peers.Length, parts[0], peerIdPrefix);
                return peers;
            }
            else
            {
                // Could be the Server ID.
                if (searchTerm.Length > 20)
                {
                    logger.LogInformation("Finding peers with for topic {}...", searchTerm);
                    var peers = GetPeersForTopic(searchTerm, _ => true, cancellationToken).ToArray();
                    logger.LogInformation("Found {} peers for topic {}", peers.Length, searchTerm);
                    return peers;
                }
            }
        }
        return Array.Empty<IPeerIdentity>();
    }
}
