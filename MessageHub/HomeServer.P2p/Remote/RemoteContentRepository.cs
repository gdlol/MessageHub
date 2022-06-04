using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Remote;

namespace MessageHub.HomeServer.P2p.Remote;

public class RemoteContentRepository : IRemoteContentRepository
{
    private readonly Config config;
    private readonly INetworkProvider networkProvider;

    public RemoteContentRepository(Config config, INetworkProvider networkProvider)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(networkProvider);

        this.config = config;
        this.networkProvider = networkProvider;
    }

    public async Task<Stream?> DownloadFileAsync(string serverName, string mediaId, CancellationToken cancellationToken)
    {
        string directoryPath = Path.Combine(config.ContentPath, serverName);
        string filePath = Path.Combine(directoryPath, mediaId);
        if (File.Exists(filePath))
        {
            return File.OpenRead(filePath);
        }

        string tempDirectory = Path.Combine(config.ContentPath, "temp");
        Directory.CreateDirectory(tempDirectory);
        string tempFilePath = Path.Combine(tempDirectory, Guid.NewGuid().ToString());
        string url = $"/_matrix/media/v3/download/{serverName}/{mediaId}";
        await networkProvider.DownloadAsync(serverName, url, tempFilePath, cancellationToken);
        if (!File.Exists(tempFilePath))
        {
            return null;
        }
        Directory.CreateDirectory(directoryPath);
        File.Copy(tempFilePath, filePath);
        File.Delete(tempFilePath);
        return File.OpenRead(filePath);
    }
}
