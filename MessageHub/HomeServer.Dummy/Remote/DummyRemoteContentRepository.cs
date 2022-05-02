using MessageHub.HomeServer.Remote;

namespace MessageHub.HomeServer.Dummy.Remote;

public class DummyRemoteContentRepository : IRemoteContentRepository
{
    private readonly IRequestHandler requestHandler;

    public DummyRemoteContentRepository(IRequestHandler requestHandler)
    {
        ArgumentNullException.ThrowIfNull(requestHandler);

        this.requestHandler = requestHandler;
    }

    public async Task<Stream?> DownloadFileAsync(string serverName, string mediaId)
    {
        string url = $"_matrix/media/v3/download/{serverName}/{mediaId}";
        var stream = await requestHandler.DownloadAsync(serverName, url);
        return stream;
    }
}
