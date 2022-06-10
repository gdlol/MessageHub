namespace MessageHub.HomeServer.P2p;

public class ContentRepository : IContentRepository
{
    private readonly string mediaPath;
    private readonly IIdentityService identityService;

    public ContentRepository(Config config, IIdentityService identityService)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(identityService);

        mediaPath = Path.Combine(config.DataPath, "Media");
        Directory.CreateDirectory(mediaPath);
        this.identityService = identityService;
    }

    public async Task<string> UploadFileAsync(Stream file)
    {
        ArgumentNullException.ThrowIfNull(file);

        string serverName = identityService.GetSelfIdentity().Id;
        string mediaId = Guid.NewGuid().ToString();
        string url = $"mxc://{serverName}/{mediaId}";
        string directoryPath = Path.Combine(mediaPath, serverName);
        Directory.CreateDirectory(directoryPath);
        string filePath = Path.Combine(directoryPath, mediaId);
        using var fileStream = File.OpenWrite(filePath);
        await file.CopyToAsync(fileStream);
        return url;
    }

    public Task<Stream?> DownloadFileAsync(string serverName, string mediaId)
    {
        Stream? result = null;
        string directoryPath = Path.Combine(mediaPath, serverName);
        string filePath = Path.Combine(directoryPath, mediaId);
        if (File.Exists(filePath))
        {
            result = File.OpenRead(filePath);
        }
        return Task.FromResult(result);
    }
}
