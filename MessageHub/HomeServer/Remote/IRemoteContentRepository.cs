namespace MessageHub.HomeServer.Remote;

public interface IRemoteContentRepository
{
    Task<Stream?> DownloadFileAsync(string serverName, string mediaId);
}
