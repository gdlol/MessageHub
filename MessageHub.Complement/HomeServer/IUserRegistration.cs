namespace MessageHub.Complement.HomeServer;

public interface IUserRegistration
{
    ValueTask<bool> TryRegisterAsync(string userName);
    ValueTask<string?> TryGetAddressAsync(string userName);
    ValueTask<string?> TryGetP2pUserIdAsync(string userName);
}
