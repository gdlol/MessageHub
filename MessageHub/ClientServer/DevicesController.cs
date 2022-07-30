using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class DevicesController : ControllerBase
{
    private readonly IAuthenticator authenticator;
    private readonly IDeviceManager deviceManager;

    public DevicesController(IAuthenticator authenticator, IDeviceManager deviceManager)
    {
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(deviceManager);

        this.authenticator = authenticator;
        this.deviceManager = deviceManager;
    }

    [Route("devices")]
    [HttpGet]
    public async Task<object> GetDevices()
    {
        var deviceIds = await authenticator.GetDeviceIdsAsync();
        var devices = new List<MatrixDevice>();
        foreach (var deviceId in deviceIds)
        {
            var device = await deviceManager.TryGetDeviceAsync(deviceId);
            device ??= new MatrixDevice
            {
                DeviceId = deviceId
            };
            devices.Add(device);
        }
        return new { devices };
    }

    [Route("devices/{deviceId}")]
    [HttpGet]
    public async Task<object> GetDevice(string deviceId)
    {
        var deviceIds = await authenticator.GetDeviceIdsAsync();
        if (!deviceIds.Contains(deviceId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        var device = await deviceManager.TryGetDeviceAsync(deviceId);
        if (device is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        return device;
    }

    [Route("devices/{deviceId}")]
    [HttpPut]
    public async Task<object> SetDevice([FromRoute] string deviceId, [FromBody] SetDeviceRequest request)
    {
        var deviceIds = await authenticator.GetDeviceIdsAsync();
        if (!deviceIds.Contains(deviceId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        await deviceManager.SetDeviceAsync(new MatrixDevice
        {
            DeviceId = deviceId,
            DisplayName = request.DisplayName,
            LastSeenIP = HttpContext.Connection.RemoteIpAddress?.ToString(),
            LastSeenTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        return new object();
    }

    [Route("devices/{deviceId}")]
    [HttpDelete]
    public async Task<object> DeleteDevice(string deviceId)
    {
        var deviceIds = await authenticator.GetDeviceIdsAsync();
        if (!deviceIds.Contains(deviceId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        await authenticator.LogOutAsync(deviceId);
        await deviceManager.DeleteDeviceAsync(deviceId);
        return new object();
    }

    [Route("delete_devices")]
    [HttpPost]
    public async Task<object> DeleteDevices(DeleteDevicesRequest request)
    {
        foreach (var deviceId in request.Devices)
        {
            await authenticator.LogOutAsync(deviceId);
            await deviceManager.DeleteDeviceAsync(deviceId);
        }
        return new object();
    }
}
