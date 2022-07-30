using System.Collections.Concurrent;
using MessageHub.ClientServer.Protocol;

namespace MessageHub.HomeServer;

public class DeviceManager : IDeviceManager
{
    private readonly ConcurrentDictionary<string, MatrixDevice> devices = new();

    public ValueTask<MatrixDevice?> TryGetDeviceAsync(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        MatrixDevice? result = null;
        if (devices.TryGetValue(deviceId, out var device))
        {
            result = device;
        }
        return ValueTask.FromResult(result);
    }

    public ValueTask SetDeviceAsync(MatrixDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        devices[device.DeviceId] = device;
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteDeviceAsync(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        devices.TryRemove(deviceId, out var _);
        return ValueTask.CompletedTask;
    }
}
