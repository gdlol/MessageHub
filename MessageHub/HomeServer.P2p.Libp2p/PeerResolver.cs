using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MessageHub.HomeServer.P2p.Libp2p;

public class PeerResolver : IPeerResolver
{
    private readonly ILogger logger;
    private readonly Host host;
    private readonly DHT dht;
    private readonly Discovery discovery;
    private readonly Func<ServerKeys, IIdentity?> tryGetIdentity;
    private readonly IMemoryCache addressCache;

    public PeerResolver(
        ILogger<PeerResolver> logger,
        Host host,
        DHT dht,
        Discovery discovery,
        Func<ServerKeys, IIdentity?> tryGetIdentity,
        IMemoryCache addressCache)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(dht);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(tryGetIdentity);
        ArgumentNullException.ThrowIfNull(addressCache);

        this.logger = logger;
        this.host = host;
        this.dht = dht;
        this.discovery = discovery;
        this.tryGetIdentity = tryGetIdentity;
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
            rendezvousPoint = $"p2p:{id}";
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
                (string, string)? cachedInfo = null;
                try
                {
                    if (addressCache.TryGetValue(id, out string addressInfo))
                    {
                        logger.LogDebug("Found address info in cache for id {}: {}", id, addressInfo);
                        string peerId = Host.GetIdFromAddressInfo(addressInfo);
                        cachedInfo = (peerId, addressInfo);
                    }
                    IEnumerable<(string, string)> GetPeers()
                    {
                        if (cachedInfo is not null)
                        {
                            yield return cachedInfo.Value;
                        }
                        var peers = discovery.FindPeers(rendezvousPoint, token);
                        foreach (var (id, info) in peers)
                        {
                            if ((id, info).Equals(cachedInfo))
                            {
                                continue;
                            }
                            yield return (id, info);
                        }
                    }
                    addressInfos = GetPeers();
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
                        var identity = tryGetIdentity(serverKeys);
                        if (identity is not null && AuthorizedPeer.Verify(identity, peerId))
                        {
                            if (tcs.TrySetResult((addressInfo, identity)))
                            {
                                cts.Cancel();
                                logger.LogDebug("Found address info for {}: {}", id, addressInfo);
                                addressCache.Set(
                                    id,
                                    addressInfo,
                                    DateTimeOffset.FromUnixTimeMilliseconds(serverKeys.ValidUntilTimestamp));
                                host.Protect(Host.GetIdFromAddressInfo(addressInfo), nameof(MessageHub));
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
                        if ((peerId, addressInfo).Equals(cachedInfo))
                        {
                            addressCache.Remove(id);
                        }
                    }
                });
                await Task.Delay(TimeSpan.FromSeconds(3), token);
            }
        }, cancellationToken);
        var result = await tcs.Task;
        return result;
    }
}
