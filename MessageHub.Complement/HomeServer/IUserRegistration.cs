namespace MessageHub.Complement.HomeServer;

public interface IUserRegistration
{
    ValueTask<string?> TryRegisterAsync(string userName);
    ValueTask<string?> TryGetAddress(string userName);
    ValueTask<string?> TryGetP2pUserId(string userName);
}
