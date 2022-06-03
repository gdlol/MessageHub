using System.Collections.Concurrent;
using System.Text.Json;
using MessageHub.Federation.Protocol;
using Microsoft.Extensions.Caching.Memory;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal class P2pNode : IDisposable
{
    public Host Host { get; }
    public DHT Dht { get; }
    public Discovery Discovery { get; }
    public MemberStore MemberStore { get; }
    public PubSub Pubsub { get; }
    public IPeerResolver Resolver { get; }
    private readonly ILogger logger;
    private readonly Func<ServerKeys, IIdentity?> identityVerifier;
    private readonly IMemoryCache addressCache;

    public P2pNode(
        Host host,
        DHT dht,
        Discovery discovery,
        MemberStore memberStore,
        PubSub pubsub,
        ILoggerFactory loggerFactory,
        Func<ServerKeys, IIdentity?> identityVerifier,
        IMemoryCache addressCache)
    {
        Host = host;
        Dht = dht;
        Discovery = discovery;
        MemberStore = memberStore;
        Pubsub = pubsub;
        Resolver = new PeerResolver(
            loggerFactory.CreateLogger<PeerResolver>(),
            host,
            dht,
            discovery,
            identityVerifier,
            addressCache);
        logger = loggerFactory.CreateLogger<P2pNode>();
        this.identityVerifier = identityVerifier;
        this.addressCache = addressCache;
    }

    public void Dispose()
    {
        Pubsub.Dispose();
        MemberStore.Dispose();
        Discovery.Dispose();
        Dht.Dispose();
    }

    public async Task<JsonElement> SendAsync(SignedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (addressInfo, _) = await Resolver.ResolveAddressInfoAsync(
            request.Destination,
            cancellationToken: cancellationToken);
        Host.Connect(addressInfo, cancellationToken);
        var peerId = Host.GetIdFromAddressInfo(addressInfo);
        var response = Host.SendRequest(peerId, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogDebug("Response status code from {}: {}", request.Destination, response.StatusCode);
        }
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);
        logger.LogDebug("Response from {} {}: {}", request.Destination, request.Uri, result);
        response.EnsureSuccessStatusCode();
        return result;
    }

    public async Task<IIdentity?> GetServerIdentityAsync(
        string peerId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting server identity of {}...", peerId);
        string addressInfo = Dht.FindPeer(peerId, cancellationToken);
        Host.Connect(addressInfo, cancellationToken);
        var response = await Host.GetServerKeysAsync(peerId, cancellationToken);
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

    public IEnumerable<IIdentity> GetPeersForTopicAsync(
        string topic,
        Func<IIdentity, bool> peerFilter,
        CancellationToken cancellationToken)
    {
        using var queue = new BlockingCollection<IIdentity>();
        Task.Run(async () =>
        {
            try
            {
                logger.LogDebug("Finding peers for topic {}...", topic);
                var addressInfos = Discovery.FindPeers(topic, cancellationToken);
                logger.LogDebug("Found {} candidate peers for topic {}.", addressInfos.Count, topic);
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = 8
                };
                await Parallel.ForEachAsync(addressInfos, parallelOptions, async (x, token) =>
                {
                    var (peerId, addressInfo) = x;
                    if (peerId == Host.Id)
                    {
                        return;
                    }
                    try
                    {
                        token.ThrowIfCancellationRequested();

                        logger.LogDebug("Connecting to {}: {}...", peerId, addressInfo);
                        Host.Connect(addressInfo, token);
                        logger.LogDebug("Connected to {}: {}", peerId, addressInfo);

                        var responseBody = await Host.GetServerKeysAsync(peerId, token);
                        var serverKeys = responseBody?.Deserialize<ServerKeys>();
                        if (serverKeys is null)
                        {
                            logger.LogDebug("Null server keys response from {}", peerId);
                            return;
                        }
                        logger.LogDebug("Server keys from {}: {}", peerId, responseBody);
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
                        logger.LogDebug("Error verifying identify of peer {}: {}", addressInfo, ex.Message);
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.LogDebug("Error finding peers for topic {}: {}", topic, ex.Message);
            }
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

    public async Task<IIdentity[]> SearchPeersAsync(
        IIdentity selfIdentity,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            return Array.Empty<IIdentity>();
        }
        logger.LogInformation("Search term: {}", searchTerm);

        string p2pPrefix = "/p2p/";
        if (searchTerm.StartsWith(p2pPrefix))
        {
            string peerId = searchTerm[p2pPrefix.Length..];
            if (peerId == Host.Id)
            {
                logger.LogInformation("Peer ID is self peer ID.");
                return new[] { selfIdentity };
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
                logger.LogInformation("Error finding peer address for {}: {}", peerId, ex.Message);
            }
        }
        else
        {
            // check for search term of the form /Display Name/abcdefg
            var parts = searchTerm.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (searchTerm.StartsWith('/') && parts.Length >= 2)
            {
                string peerIdPrefix = parts[^1];
                string name = searchTerm[..^peerIdPrefix.Length].Trim('/');
                logger.LogInformation(
                    "Finding peers with name {} and peer ID prefix {}...",
                    name,
                    peerIdPrefix);
                bool verifyIdentity(IIdentity identity)
                {
                    if (identity.VerifyKeys.Keys.TryGetValue(AuthenticatedPeer.KeyIdentifier, out var key))
                    {
                        return key.StartsWith(peerIdPrefix);
                    }
                    return false;
                }
                try
                {
                    var peers = GetPeersForTopicAsync(searchTerm, verifyIdentity, cancellationToken).ToArray();
                    logger.LogInformation(
                        "Found {} peers with name {} and peer ID prefix {}", peers.Length, name, peerIdPrefix);
                    return peers;
                }
                catch (Exception ex)
                {
                    logger.LogInformation("Error finding peers with name {} and peer ID prefix {}: {}",
                        name,
                        peerIdPrefix,
                        ex.Message);
                }
            }
            else
            {
                if (searchTerm.Length > 20)
                {
                    string id = searchTerm;
                    if (searchTerm == selfIdentity.Id)
                    {
                        logger.LogInformation("ID is self ID.");
                        return new[] { selfIdentity };
                    }
                    logger.LogInformation("Finding peer address for {}...", id);
                    try
                    {
                        var (_, identity) = await Resolver.ResolveAddressInfoAsync(
                            id, cancellationToken: cancellationToken);
                        return new[] { identity };
                    }
                    catch (Exception ex)
                    {
                        logger.LogInformation("Error finding peer address for {}: {}", id, ex.Message);
                    }
                }
            }
        }
        return Array.Empty<IIdentity>();
    }
}
