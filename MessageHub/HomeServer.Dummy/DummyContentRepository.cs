using System.Collections.Concurrent;

namespace MessageHub.HomeServer.Dummy;

public class DummyContentRepository : IContentRepository
{
    private readonly ConcurrentDictionary<string, byte[]> files = new();

    public async Task<string> UploadFileAsync(Stream file)
    {
        ArgumentNullException.ThrowIfNull(file);

        string url = $"mxc://{DummyHostInfo.Instance.ServerName}/{Guid.NewGuid()}";
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        if (!files.TryAdd(url, memoryStream.ToArray()))
        {
            throw new InvalidOperationException();
        }
        return url;
    }

    public Task<Stream?> DownloadFileAsync(string url)
    {
        Stream? result = null;
        if (files.TryGetValue(url, out var bytes))
        {
            result = new MemoryStream(bytes);
        }
        return Task.FromResult(result);
    }
}
