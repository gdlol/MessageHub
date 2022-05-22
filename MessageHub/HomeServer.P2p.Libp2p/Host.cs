using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public class HostConfig
{
    public bool AdvertisePrivateAddresses { get; init; }
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

    public string GetHostAddressInfo()
    {
        using var error = NativeMethods.GetHostAddressInfo(handle, out var resultJSON);
        LibP2pException.Check(error);
        using var _ = resultJSON;
        return resultJSON.ToString();
    }

    public void Connect(string addressInfo, CancellationToken cancellationToken = default)
    {
        using var context = new Context(cancellationToken);
        using var addrInfo = StringHandle.FromString(addressInfo);
        using var error = NativeMethods.ConnectHost(context.Handle, handle, addrInfo);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
    }

    public HttpResponseMessage SendRequest(string peerId, SignedRequest request, CancellationToken cancellationToken = default)
    {
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

    public Proxy StartProxyRequests(string proxy)
    {
        using var proxyString = StringHandle.FromString(proxy);
        using var error = NativeMethods.StartProxyRequests(handle, proxyString, out var proxyHandle);
        LibP2pException.Check(error);
        return new Proxy(proxyHandle);
    }
}
