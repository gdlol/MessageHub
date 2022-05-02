namespace MessageHub.HomeServer.Dummy;

public class DummyUserProfile : IUserProfile
{
    private string? avatarUrl;
    private string? displayName;

    public Task<string?> GetAvatarUrlAsync(string userId)
    {
        return Task.FromResult(avatarUrl);
    }

    public Task<string?> GetDisplayNameAsync(string userId)
    {
        return Task.FromResult(displayName);
    }

    public Task SetAvatarUrlAsync(string userId, string url)
    {
        avatarUrl = url;
        return Task.CompletedTask;
    }

    public Task SetDisplayNameAsync(string userId, string name)
    {
        displayName = name;
        return Task.CompletedTask;
    }
}
