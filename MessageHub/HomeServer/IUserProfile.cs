namespace MessageHub.HomeServer;

public interface IUserProfile
{
    public Task<string?> GetDisplayNameAsync(string userId);
    public Task<string?> GetAvatarUrlAsync(string userId);
    public Task SetDisplayNameAsync(string userId, string name);
    public Task SetAvatarUrlAsync(string userId, string url);
}
