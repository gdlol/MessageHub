using System.Collections.Concurrent;
using System.Text.Json;
using MessageHub.Federation.Protocol;
using Microsoft.Extensions.Caching.Memory;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal class P2pNode : IDisposable
{
    public Host Host { get; }
    public DHT DHT { get; }
    public Discovery Discovery { get; }
    public MemberStore MemberStore { get; }
    public PubSub Pubsub { get; }
    public IPeerResolver Resolver { get; }
    private readonly ILogger logger;
    private readonly Func<ServerKeys, IIdentity?> tryGetIdentity;
    private readonly IMemoryCache addressCache;

    public P2pNode(
        Host host,
        DHT dht,
        Discovery discovery,
        MemberStore memberStore,
        PubSub pubsub,
        ILoggerFactory loggerFactory,
        Func<ServerKeys, IIdentity?> tryGetIdentity,
        IMemoryCache addressCache)
    {
        Host = host;
        DHT = dht;
        Discovery = discovery;
        MemberStore = memberStore;
        Pubsub = pubsub;
        Resolver = new PeerResolver(
            loggerFactory.CreateLogger<PeerResolver>(),
            host,
            dht,
            discovery,
            tryGetIdentity,
            addressCache);
        logger = loggerFactory.CreateLogger<P2pNode>();
        this.tryGetIdentity = tryGetIdentity;
        this.addressCache = addressCache;
    }

    public void Dispose()
    {
        Pubsub.Dispose();
        MemberStore.Dispose();
        Discovery.Dispose();
        DHT.Dispose();
    }

    public IIdentity? TryGetIdentity(ServerKeys serverKeys)
    {
        return tryGetIdentity(serverKeys);
    }

    public IIdentity? VerifyIdentity(ServerKeys serverKeys, string peerId)
    {
        var identity = tryGetIdentity(serverKeys);
        if (identity is not null && AuthorizedPeer.Verify(identity, peerId))
        {
            return identity;
        }
        return null;
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
        string? addressInfo = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            addressInfo = DHT.FindPeer(peerId, cancellationToken);
            if (addressInfo is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            else
            {
                break;
            }
        }
        Host.Connect(addressInfo, cancellationToken);
        var response = await Host.GetServerKeysAsync(peerId, cancellationToken);
        var serverKeys = response?.Deserialize<ServerKeys>();
        if (serverKeys is null)
        {
            logger.LogDebug("Null server keys response from {}", peerId);
            return null;
        }
        var identity = VerifyIdentity(serverKeys, peerId);
        if (identity is not null)
        {
            addressCache.Set(
                identity.Id,
                addressInfo,
                DateTimeOffset.FromUnixTimeMilliseconds(serverKeys.ValidUntilTimestamp));
            Host.Protect(peerId, nameof(MessageHub));
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
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = cts.Token;
        using var queue = new BlockingCollection<IIdentity>();
        Task.Run(async () =>
        {
            try
            {
                logger.LogDebug("Finding peers for topic {}...", topic);
                var addressInfos = Discovery.FindPeers(topic, token);
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = token,
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
                        var identity = VerifyIdentity(serverKeys, peerId);
                        if (identity is not null)
                        {
                            if (peerFilter(identity))
                            {
                                logger.LogDebug("Peer found for topic {}: {}", topic, identity.Id);
                                queue.TryAdd(identity);
                                if (Host.TryGetAddressInfo(peerId) is string info)
                                {
                                    addressCache.Set(
                                        identity.Id,
                                        info,
                                        DateTimeOffset.FromUnixTimeMilliseconds(serverKeys.ValidUntilTimestamp));
                                    Host.Protect(peerId, nameof(MessageHub));
                                }
                                cts.CancelAfter(TimeSpan.FromSeconds(1));
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

    public async Task<IEnumerable<IIdentity>> SearchPeersAsync(
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
            if (peerId.Length < 20)
            {
                return Array.Empty<IIdentity>();
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
            if (searchTerm.StartsWith('@'))
            {
                searchTerm = searchTerm[1..];
            }
            // check for search term of the form "Display Name:abcdefg"
            var parts = searchTerm.Split(':');
            if (parts.Length >= 2)
            {
                string idSuffix = parts[^1];
                if (idSuffix.Length < 7)
                {
                    return Array.Empty<IIdentity>();
                }
                string name = searchTerm[..^(idSuffix.Length + 1)];
                logger.LogInformation(
                    "Finding peers with name {} and ID suffix {}...",
                    name,
                    idSuffix);
                bool filterIdSuffix(IIdentity identity)
                {
                    return identity.Id.EndsWith(idSuffix);
                }
                try
                {
                    var peers = GetPeersForTopicAsync(searchTerm, filterIdSuffix, cancellationToken);
                    return peers;
                }
                catch (Exception ex)
                {
                    logger.LogInformation("Error finding peers with name {} and ID suffix {}: {}",
                        name,
                        idSuffix,
                        ex.Message);
                }
            }
            else
            {
                // Ignore for now.
            }
        }
        return Array.Empty<IIdentity>();
    }
}
