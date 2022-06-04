namespace MessageHub.HomeServer;

public interface IContentRepository
{
    Task<string> UploadFileAsync(Stream file);
    Task<Stream?> DownloadFileAsync(string serverName, string mediaId);
}
