using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.P2p.Libp2p;

public class PeerResolver : IPeerResolver
{
    private readonly ILogger logger;
    private readonly IPeerIdentity peerIdentity;
    private readonly Host host;
    private readonly Discovery discovery;
    private readonly Func<ServerKeys, IPeerIdentity?> identityVerifier;

    public PeerResolver(
        ILoggerFactory loggerFactory,
        IPeerIdentity peerIdentity,
        Host host,
        Discovery discovery,
        Func<ServerKeys, IPeerIdentity?> identityVerifier)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(identityVerifier);

        logger = loggerFactory.CreateLogger<PeerResolver>();
        this.peerIdentity = peerIdentity;
        this.host = host;
        this.discovery = discovery;
        this.identityVerifier = identityVerifier;
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
                Dictionary<string, string> addressInfos;
                try
                {
                    addressInfos = discovery.FindPeers(rendezvousPoint, cts.Token);
                }
                catch (Exception ex)
                {
                    logger.LogInformation(ex, "Error finding peers for Id {}", id);
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    continue;
                }
                logger.LogDebug("Found {} candidate peers for Id: {}...", addressInfos.Count, id);
                await Parallel.ForEachAsync(addressInfos, parallelOptions, async (x, token) =>
                {
                    var (peerId, addressInfo) = x;
                    try
                    {
                        token.ThrowIfCancellationRequested();

                        logger.LogDebug("Connecting to {}: {}...", peerId, addressInfo);
                        host.Connect(addressInfo, token);
                        logger.LogDebug("Connected to {}: {}", peerId, addressInfo);

                        logger.LogDebug("Sending request to {}...", peerId);
                        var response = host.SendRequest(peerId, new SignedRequest
                        {
                            Method = HttpMethod.Get.ToString(),
                            Uri = $"/_matrix/key/v2/server",
                            Origin = "dummy",
                            OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            Destination = peerId,
                            ServerKeys = peerIdentity.GetServerKeys(),
                            Signatures = JsonSerializer.SerializeToElement(new Signatures
                            {
                                ["dummy"] = new ServerSignatures
                                {
                                    [new KeyIdentifier("dummy", "dummy")] = "dummy"
                                }
                            })
                        }, token);
                        logger.LogDebug("Sent request to {}", peerId);
                        response.EnsureSuccessStatusCode();
                        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>(
                            cancellationToken: token);
                        var serverKeys = JsonSerializer.Deserialize<ServerKeys>(responseBody);
                        if (serverKeys is not null)
                        {
                            logger.LogDebug("Server keys {}: {}", responseBody, peerId);
                            var identity = identityVerifier(serverKeys);
                            if (identity?.Id == id
                                && identity.VerifyKeys.Keys.TryGetValue(
                                    new KeyIdentifier("libp2p", "PeerID"),
                                    out var key)
                                && peerId == key)
                            {
                                tcs.TrySetResult(addressInfo);
                                cts.Cancel();
                                logger.LogDebug("Found address info for {}: {}", id, addressInfo);
                            }
                            else
                            {
                                if (identity is null)
                                {
                                    logger.LogDebug("identity not verified for {}: {}", id, responseBody);
                                }
                                else
                                {
                                    logger.LogDebug("transport not verified for {}: {}", id, responseBody);
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        logger.LogInformation(
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
