using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MessageHub.HomeServer.P2p.Libp2p;

public class PeerResolver : IPeerResolver
{
    private readonly ILogger logger;
    private readonly Host host;
    private readonly Discovery discovery;
    private readonly Func<ServerKeys, IIdentity?> identityVerifier;
    private readonly IMemoryCache addressCache;

    public PeerResolver(
        ILoggerFactory loggerFactory,
        Host host,
        Discovery discovery,
        Func<ServerKeys, IIdentity?> identityVerifier,
        IMemoryCache addressCache)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(identityVerifier);

        logger = loggerFactory.CreateLogger<PeerResolver>();
        this.host = host;
        this.discovery = discovery;
        this.identityVerifier = identityVerifier;
        this.addressCache = addressCache;
    }

    public async Task<string> ResolveAddressInfoAsync(
        string id,
        string? rendezvousPoint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (rendezvousPoint is null)
        {
            rendezvousPoint = id;
        }
        var tcs = new TaskCompletionSource<string>();
        using var _ = cancellationToken.Register(() => tcs.TrySetCanceled());
        var __ = Task.Run(async () =>
        {
            while (!tcs.Task.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMinutes(1));
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = cts.Token,
                    MaxDegreeOfParallelism = 8
                };

                logger.LogDebug("Finding peers for Id {}...", id);
                IEnumerable<(string, string)> addressInfos;
                try
                {
                    if (addressCache.TryGetValue(rendezvousPoint, out string addressInfo))
                    {
                        logger.LogDebug("Found address info in cache Id {}: {}", id, addressInfo);
                        string peerId = Host.GetIdFromAddressInfo(addressInfo);
                        IEnumerable<(string, string)> GetPeers()
                        {
                            yield return (peerId, addressInfo);
                            var peers = discovery.FindPeers(rendezvousPoint, cts.Token);
                            foreach (var (id, info) in peers)
                            {
                                yield return (id, info);
                            }
                        }
                        addressInfos = GetPeers();
                    }
                    else
                    {
                        addressInfos = discovery.FindPeers(rendezvousPoint, cts.Token)
                            .AsEnumerable()
                            .Select(x => (x.Key, x.Value))
                            .ToArray();
                        logger.LogDebug("Found {} candidate peers for Id: {}...", addressInfos.Count(), id);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogInformation(ex, "Error finding peers for Id {}", id);
                    await Task.Delay(TimeSpan.FromSeconds(3));
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
                        logger.LogDebug("Server keys {}: {}", responseBody, peerId);
                        var identity = identityVerifier(serverKeys);
                        if (identity?.Id == id
                            && identity.VerifyKeys.Keys.TryGetValue(new KeyIdentifier("libp2p", "PeerID"), out var key)
                            && peerId == key)
                        {
                            tcs.TrySetResult(addressInfo);
                            cts.Cancel();
                            logger.LogDebug("Found address info for {}: {}", id, addressInfo);
                            addressCache.Set(
                                rendezvousPoint,
                                addressInfo,
                                DateTimeOffset.FromUnixTimeMilliseconds(serverKeys.ValidUntilTimestamp));
                        }
                        else
                        {
                            if (identity is null)
                            {
                                logger.LogDebug("identity verification failed for {}: {}", peerId, responseBody);
                            }
                            else
                            {
                                logger.LogDebug("transport verification failed for {}: {}", peerId, responseBody);
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        logger.LogDebug(
                            ex,
                            "Error verifying identify of peer {} for id {}",
                            (peerId, addressInfo), id);
                    }
                });
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }, cancellationToken);
        var result = await tcs.Task;
        return result;
    }
}
