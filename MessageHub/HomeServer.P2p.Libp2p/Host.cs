using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public class HostConfig
{
    public bool AdvertisePrivateAddresses { get; init; }
    public string[]? StaticRelays { get; init; }
    public string DataPath { get; init; } = default!;
    public string? PrivateNetworkSecret { get; init; }
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
        var error = NativeMethods.CreateHost(configJson, out var handle);
        LibP2pException.Check(error);
        return new Host(handle);
    }

    public void Dispose()
    {
        handle.Dispose();
    }

    public string Id => NativeMethods.GetHostID(handle).ToString() ?? string.Empty;
}
