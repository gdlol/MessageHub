using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.Serialization;

public static class DefaultJsonSerializer
{
    private static readonly JsonSerializerOptions options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonElement SerializeToElement<TValue>(TValue value)
    {
        return JsonSerializer.SerializeToElement(value, options);
    }

    public static Task SerializeAsync<TValue>(
        Stream utf8Stream,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        return JsonSerializer.SerializeAsync(utf8Stream, value, options, cancellationToken);
    }

    public static byte[] SerializeToUtf8Bytes<TValue>(TValue value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, options);
    }
}
