namespace MessageHub.HomeServer.P2p;

public class ContentRepository : IContentRepository
{
    private readonly Config config;
    private readonly IPeerIdentity peerIdentity;

    public ContentRepository(Config config, IPeerIdentity peerIdentity)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(peerIdentity);

        this.config = config;
        this.peerIdentity = peerIdentity;
    }

    public async Task<string> UploadFileAsync(Stream file)
    {
        ArgumentNullException.ThrowIfNull(file);

        string guid = Guid.NewGuid().ToString();
        string url = $"mxc://{peerIdentity.Id}/{guid}";
        using var fileStream = File.OpenWrite(Path.Combine(config.ContentPath, guid));
        await file.CopyToAsync(fileStream);
        return url;
    }

    public Task<Stream?> DownloadFileAsync(string url)
    {
        Stream? result = null;
        if (url.Contains('/'))
        {
            string fileName = url.Split('/')[^1];
            result = File.OpenRead(Path.Combine(config.ContentPath, fileName));
        }
        return Task.FromResult(result);
    }
}
