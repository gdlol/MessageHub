namespace MessageHub.HomeServer;

public interface IAuthenticator
{
    string GetSsoRedirectUrl(string redirectUrl);

    Task<string?> GetDeviceIdAsync(string accessToken);

    Task<(string userId, string accessToken)?> LogInAsync(string deviceId, string token);

    Task<string?> AuthenticateAsync(string accessToken);

    Task<int> LogOutAsync(string deviceId);

    Task LogOutAllAsync();
}
