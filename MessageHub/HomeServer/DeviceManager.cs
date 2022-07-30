using System.Collections.Concurrent;
using MessageHub.Authentication;
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

public class DeviceMonitorMiddleware : IMiddleware
{
    private readonly ILogger logger;
    private readonly IAuthenticator authenticator;
    private readonly IDeviceManager deviceManager;

    public DeviceMonitorMiddleware(
        ILogger<DeviceMonitorMiddleware> logger,
        IAuthenticator authenticator,
        IDeviceManager deviceManager)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(deviceManager);

        this.logger = logger;
        this.authenticator = authenticator;
        this.deviceManager = deviceManager;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.TryGetAccessToken(out string? accessToken))
        {
            string? deviceId = await authenticator.GetDeviceIdAsync(accessToken);
            if (deviceId is null)
            {
                logger.LogWarning("Device id not found for access token");
            }
            else
            {
                var device = await deviceManager.TryGetDeviceAsync(deviceId);
                if (device is null)
                {
                    device = new MatrixDevice
                    {
                        DeviceId = deviceId,
                        LastSeenIP = context.Connection.RemoteIpAddress?.ToString(),
                        LastSeenTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                }
                else
                {
                    device = device with
                    {
                        LastSeenIP = context.Connection.RemoteIpAddress?.ToString(),
                        LastSeenTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                }
                await deviceManager.SetDeviceAsync(device);
            }
        }
        await next(context);
    }
}
