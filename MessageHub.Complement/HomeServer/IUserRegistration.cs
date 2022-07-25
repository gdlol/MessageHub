namespace MessageHub.Complement.HomeServer;

public interface IUserRegistration
{
    ValueTask<bool> TryRegisterAsync(string userName, string password);
    ValueTask<bool> VerifyUserAsync(string userName, string password);
    ValueTask<string?> TryGetAddressAsync(string userName);
    ValueTask<string?> TryGetP2pUserIdAsync(string userName);
}
