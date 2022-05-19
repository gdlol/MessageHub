using MessageHub.HomeServer.Remote;

namespace MessageHub.HomeServer.P2p.Remote;

public class RemoteContentRepository : IRemoteContentRepository
{
    private readonly IRequestHandler requestHandler;

    public RemoteContentRepository(IRequestHandler requestHandler)
    {
        ArgumentNullException.ThrowIfNull(requestHandler);

        this.requestHandler = requestHandler;
    }

    public async Task<Stream?> DownloadFileAsync(string serverName, string mediaId)
    {
        string url = $"/_matrix/media/v3/download/{serverName}/{mediaId}";
        var stream = await requestHandler.DownloadAsync(serverName, url);
        return stream;
    }
}

