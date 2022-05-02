using System.Text.Json;
using MessageHub.HomeServer.Remote;

namespace MessageHub.HomeServer.Dummy.Remote;

public class DummyRequestHandler : IRequestHandler
{
    public Task<Stream> DownloadAsync(string peerId, string url)
    {
        throw new NotSupportedException();
    }

    public Task<JsonElement> SendRequest(JsonElement signedRequest)
    {
        JsonElement result = JsonSerializer.SerializeToElement<object?>(null);
        return Task.FromResult(result);
    }
}
