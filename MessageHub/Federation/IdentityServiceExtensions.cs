using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;

namespace MessageHub.Federation;

public static class IdentityServiceExtensions
{
    private static readonly JsonSerializerOptions ignoreNullOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static SignedRequest SignRequest(
        this IIdentity identity,
        string destination,
        string requestMethod,
        string requestTarget,
        long? timestamp = null,
        object? content = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(requestMethod);
        ArgumentNullException.ThrowIfNull(requestTarget);

        var request = new SignedRequest
        {
            Method = requestMethod,
            Uri = requestTarget,
            Origin = identity.Id,
            OriginServerTimestamp = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Destination = destination,
            ServerKeys = identity.GetServerKeys(),
            Signatures = JsonSerializer.SerializeToElement<object?>(null)
        };
        if (content is not null)
        {
            request.Content = JsonSerializer.SerializeToElement(content, ignoreNullOptions);
        }
        var element = JsonSerializer.SerializeToElement(request, ignoreNullOptions);
        element = identity.SignJson(element);
        return element.Deserialize<SignedRequest>()!;
    }

    public static JsonElement SignResponse(this IIdentity identity, SignedRequest request, object content)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);

        var response = new SignedResponse
        {
            Request = request,
            Content = JsonSerializer.SerializeToElement(content, ignoreNullOptions),
            Signatures = JsonSerializer.SerializeToElement<object?>(null)
        };
        var element = JsonSerializer.SerializeToElement(response, ignoreNullOptions);
        return identity.SignJson(element);
    }
}
