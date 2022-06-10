using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Remote;

namespace MessageHub.HomeServer.P2p.Remote;

public class RemoteContentRepository : IRemoteContentRepository
{
    private readonly string mediaPath;
    private readonly INetworkProvider networkProvider;

    public RemoteContentRepository(Config config, INetworkProvider networkProvider)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(networkProvider);

        mediaPath = Path.Combine(config.DataPath, "Media");
        Directory.CreateDirectory(mediaPath);
        this.networkProvider = networkProvider;
    }

    public async Task<Stream?> DownloadFileAsync(string serverName, string mediaId, CancellationToken cancellationToken)
    {
        string directoryPath = Path.Combine(mediaPath, serverName);
        string filePath = Path.Combine(directoryPath, mediaId);
        if (File.Exists(filePath))
        {
            return File.OpenRead(filePath);
        }

        string tempDirectory = Path.Combine(mediaPath, "temp");
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
