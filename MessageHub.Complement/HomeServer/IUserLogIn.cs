using MessageHub.ClientServer.Protocol;

namespace MessageHub.Complement.HomeServer;

public interface IUserLogIn
{
    Task<LogInResponse> LogInAsync(string userName, string? deviceId, string? deviceName);
    Task<string?> TryGetDeviceIdAsync(string accessToken);
    Task<string?> TryGetUserNameAsync(string accessToken);
}
