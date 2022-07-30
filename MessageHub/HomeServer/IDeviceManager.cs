using MessageHub.ClientServer.Protocol;

namespace MessageHub.HomeServer;

public interface IDeviceManager
{
    ValueTask<MatrixDevice?> TryGetDeviceAsync(string deviceId);
    ValueTask SetDeviceAsync(MatrixDevice device);
    ValueTask DeleteDeviceAsync(string deviceId);
}
