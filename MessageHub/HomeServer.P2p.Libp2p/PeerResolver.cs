using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MessageHub.HomeServer.P2p.Libp2p;

public class PeerResolver : IPeerResolver
{
    private readonly ILogger logger;
    private readonly Host host;
    private readonly DHT dht;
    private readonly Discovery discovery;
    private readonly Func<ServerKeys, IIdentity?> identityVerifier;
    private readonly IMemoryCache addressCache;

    public PeerResolver(
        ILogger<PeerResolver> logger,
        Host host,
        DHT dht,
        Discovery discovery,
        Func<ServerKeys, IIdentity?> identityVerifier,
        IMemoryCache addressCache)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(dht);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(identityVerifier);
        ArgumentNullException.ThrowIfNull(addressCache);

        this.logger = logger;
        this.host = host;
        this.dht = dht;
        this.discovery = discovery;
        this.identityVerifier = identityVerifier;
        this.addressCache = addressCache;
    }

    public async Task<(string, IIdentity)> ResolveAddressInfoAsync(
        string id,
        string? rendezvousPoint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (rendezvousPoint is null)
        {
            rendezvousPoint = id;
        }
        var tcs = new TaskCompletionSource<(string, IIdentity)>();
        using var _ = cancellationToken.Register(() => tcs.TrySetCanceled());
        var __ = Task.Run(async () =>
        {
            logger.LogDebug("Finding peers for id {}...", id);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(1));
            var token = cts.Token;
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = 8
            };
            while (!tcs.Task.IsCompleted)
            {
                token.ThrowIfCancellationRequested();

                IEnumerable<(string, string)> addressInfos;
                try
                {
                    if (addressCache.TryGetValue(id, out string addressInfo))
                    {
                        logger.LogDebug("Found address info in cache for id {}: {}", id, addressInfo);
                        string peerId = Host.GetIdFromAddressInfo(addressInfo);
                        IEnumerable<(string, string)> GetPeers()
                        {
                            yield return (peerId, addressInfo);
                            var peers = discovery.FindPeers(rendezvousPoint, token);
                            foreach (var (id, info) in peers)
                            {
                                if ((id, info).Equals((peerId, addressInfo)))
                                {
                                    continue;
                                }
                                yield return (id, info);
                            }
                        }
                        addressInfos = GetPeers();
                    }
                    else
                    {
                        addressInfos = discovery.FindPeers(rendezvousPoint, token)
                            .AsEnumerable()
                            .Select(x => (x.Key, x.Value))
                            .ToArray();
                        logger.LogDebug("Found {} candidate peers for id: {}...", addressInfos.Count(), id);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogDebug("Error finding peers for id {}: {}", id, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                    continue;
                }
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
                        logger.LogDebug("Server keys from {}: {}", peerId, responseBody);
                        var identity = identityVerifier(serverKeys);
                        if (identity?.Id == id
                            && identity.VerifyKeys.Keys.TryGetValue(
                                AuthenticatedPeer.KeyIdentifier,
                                out var authenticatedPeerId))
                        {
                            logger.LogDebug("Found authenticated peer ID for {}: {}", id, authenticatedPeerId);
                            if (peerId != authenticatedPeerId)
                            {
                                addressInfo = dht.FindPeer(authenticatedPeerId, token);

                                logger.LogDebug("Connecting to {}: {}...", authenticatedPeerId, addressInfo);
                                host.Connect(addressInfo, token);
                                logger.LogDebug("Connected to {}: {}", authenticatedPeerId, addressInfo);
                            }
                            if (tcs.TrySetResult((addressInfo, identity)))
                            {
                                cts.Cancel();
                                logger.LogDebug("Found address info for {}: {}", id, addressInfo);
                                addressCache.Set(
                                    id,
                                    addressInfo,
                                    DateTimeOffset.FromUnixTimeMilliseconds(serverKeys.ValidUntilTimestamp));
                            }
                        }
                        else
                        {
                            logger.LogDebug("identity verification failed for {}", peerId);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(
                            "Error verifying identify of peer {} for id {}: {}",
                            (peerId, addressInfo), id, ex.Message);
                    }
                });
                await Task.Delay(TimeSpan.FromSeconds(3), token);
            }
        }, cancellationToken);
        var result = await tcs.Task;
        return result;
    }
}
