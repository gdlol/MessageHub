using System.Text.Json;
using MessageHub.Federation.Protocol;

namespace MessageHub.HomeServer.Remote;

public interface IRequestHandler
{
    Task<JsonElement> SendRequest(SignedRequest signedRequest);
    Task<Stream> DownloadAsync(string id, string url);
}
