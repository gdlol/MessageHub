using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public class HostConfig
{
    public string[]? StaticRelays { get; init; }
    public string DataPath { get; init; } = default!;
    public string? PrivateNetworkSecret { get; init; }
}

public sealed class Proxy : IDisposable
{
    private readonly ProxyHandle handle;

    internal ProxyHandle Handle => handle;

    internal Proxy(ProxyHandle handle)
    {
        this.handle = handle;
    }

    public void Dispose()
    {
        handle.Dispose();
    }

    public void Stop()
    {
        using var error = NativeMethods.StopProxyRequests(handle);
        LibP2pException.Check(error);
    }
}

public sealed class Host : IDisposable
{
    private readonly HostHandle handle;

    internal HostHandle Handle => handle;

    private Host(HostHandle handle)
    {
        this.handle = handle;
    }

    public static Host Create(HostConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        using var configJson = StringHandle.FromUtf8Bytes(JsonSerializer.SerializeToUtf8Bytes(config, options));
        using var error = NativeMethods.CreateHost(configJson, out var handle);
        LibP2pException.Check(error);
        return new Host(handle);
    }

    public void Dispose()
    {
        handle.Dispose();
    }

    public string Id => NativeMethods.GetHostID(handle).ToString();

    public string GetSelfAddressInfo()
    {
        using var error = NativeMethods.GetHostAddressInfo(handle, out var resultJSON);
        LibP2pException.Check(error);
        using var _ = resultJSON;
        return resultJSON.ToString();
    }

    public static string GetIdFromAddressInfo(string addressInfo)
    {
        using var addressInfoString = StringHandle.FromString(addressInfo);
        using var error = NativeMethods.GetIDFromAddressInfo(addressInfoString, out var id);
        LibP2pException.Check(error);
        using var _ = id;
        return id.ToString();
    }

    public static bool IsValidAddressInfo(string addressInfo)
    {
        using var addressInfoString = StringHandle.FromString(addressInfo);
        using var error = NativeMethods.IsValidAddressInfo(addressInfoString, out IntPtr result);
        LibP2pException.Check(error);
        return !result.Equals(IntPtr.Zero);
    }

    public string? TryGetAddressInfo(string peerId)
    {
        ArgumentNullException.ThrowIfNull(peerId);

        using var peerIdString = StringHandle.FromString(peerId);
        using var error = NativeMethods.GetPeerInfo(handle, peerIdString, out var resultJSON);
        LibP2pException.Check(error);
        if (resultJSON.IsInvalid)
        {
            return null;
        }
        using var _ = resultJSON;
        return resultJSON.ToString();
    }

    public void Connect(string addressInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addressInfo);

        using var context = new Context(cancellationToken);
        using var addrInfo = StringHandle.FromString(addressInfo);
        using var error = NativeMethods.ConnectHost(context.Handle, handle, addrInfo);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
    }

    public void Protect(string peerId, string tag)
    {
        using var peerIdString = StringHandle.FromString(peerId);
        using var tagString = StringHandle.FromString(tag);
        using var error = NativeMethods.ProtectPeer(handle, peerIdString, tagString);
        LibP2pException.Check(error);
    }

    public int ConnectToSavedPeers(CancellationToken cancellationToken)
    {
        using var context = new Context(cancellationToken);
        using var count = NativeMethods.ConnectToSavedPeers(context.Handle, handle);
        return int.Parse(count.ToString());
    }

    public HttpResponseMessage SendRequest(
        string peerId,
        SignedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(peerId);
        ArgumentNullException.ThrowIfNull(request);

        using var context = new Context(cancellationToken);
        using var peerIdString = StringHandle.FromString(peerId);
        using var signedRequestJson = StringHandle.FromUtf8Bytes(JsonSerializer.SerializeToUtf8Bytes(request));
        using var error = NativeMethods.SendRequest(
            context.Handle,
            handle,
            peerIdString,
            signedRequestJson,
            out int responseStatus,
            out var responseBody);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
        using var _ = responseBody;
        var response = new HttpResponseMessage((HttpStatusCode)responseStatus);
        if (response.IsSuccessStatusCode)
        {
            var body = JsonSerializer.Deserialize<JsonElement>(responseBody.ToString());
            response.Content = JsonContent.Create(body);
        }
        else
        {
            response.Content = new StringContent(responseBody.ToString());
        };
        return response;
    }

    public async Task<JsonElement?> GetServerKeysAsync(string peerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(peerId);

        var response = SendRequest(peerId, new SignedRequest
        {
            Method = HttpMethod.Get.ToString(),
            Uri = $"/_matrix/key/v2/server",
            Origin = "dummy",
            Destination = peerId,
            ServerKeys = new ServerKeys { ServerName = "dummy" },
            Signatures = JsonSerializer.SerializeToElement(new Signatures
            {
                ["dummy"] = new ServerSignatures
                {
                    [new KeyIdentifier("dummy", "dummy")] = "dummy"
                }
            })
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);
        return result;
    }

    public Proxy StartProxyRequests(string proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        using var proxyString = StringHandle.FromString(proxy);
        using var error = NativeMethods.StartProxyRequests(handle, proxyString, out var proxyHandle);
        LibP2pException.Check(error);
        return new Proxy(proxyHandle);
    }

    public void DownloadFile(string peerId, string url, string filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(peerId);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(filePath);

        using var context = new Context(cancellationToken);
        using var peerIdString = StringHandle.FromString(peerId);
        using var urlString = StringHandle.FromString(url);
        using var filePathString = StringHandle.FromString(filePath);
        using var error = NativeMethods.DownloadFile(context.Handle, handle, peerIdString, urlString, filePathString);
        LibP2pException.Check(error);
    }
}
