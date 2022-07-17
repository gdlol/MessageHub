using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using MessageHub.Serialization;

namespace MessageHub.Federation;

public static class IdentityServiceExtensions
{
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
            Signatures = DefaultJsonSerializer.SerializeToElement<object?>(null)
        };
        if (content is not null)
        {
            request.Content = DefaultJsonSerializer.SerializeToElement(content);
        }
        var element = DefaultJsonSerializer.SerializeToElement(request);
        element = identity.SignJson(element, request.OriginServerTimestamp);
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
            Content = DefaultJsonSerializer.SerializeToElement(content),
            Signatures = DefaultJsonSerializer.SerializeToElement<object?>(null)
        };
        var element = DefaultJsonSerializer.SerializeToElement(response);
        return identity.SignJson(element, request.OriginServerTimestamp);
    }
}
