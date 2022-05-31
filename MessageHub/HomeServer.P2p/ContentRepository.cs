namespace MessageHub.HomeServer.P2p;

public class ContentRepository : IContentRepository
{
    private readonly Config config;
    private readonly IIdentityService identityService;

    public ContentRepository(Config config, IIdentityService identityService)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(identityService);

        this.config = config;
        this.identityService = identityService;
    }

    public async Task<string> UploadFileAsync(Stream file)
    {
        ArgumentNullException.ThrowIfNull(file);

        string guid = Guid.NewGuid().ToString();
        string url = $"mxc://{identityService.GetSelfIdentity().Id}/{guid}";
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
