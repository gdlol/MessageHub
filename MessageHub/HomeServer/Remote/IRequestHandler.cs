using System.Text.Json;

namespace MessageHub.HomeServer.Remote;

public interface IRequestHandler
{
    Task<JsonElement> SendRequest(JsonElement signedRequest);
    Task<Stream> DownloadAsync(string peerId, string url);
}
