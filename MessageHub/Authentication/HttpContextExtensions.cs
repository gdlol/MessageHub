using System.Diagnostics.CodeAnalysis;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;

namespace MessageHub.Authentication;

public static class HttpContextExtensions
{
    private static class ItemKeys
    {
        public const string MatrixError = nameof(MatrixError);
        public const string AccessToken = nameof(AccessToken);
        public const string SignedRequest = nameof(SignedRequest);
    }

    public static void SetMatrixError(this HttpContext context, MatrixError error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        context.Items[ItemKeys.MatrixError] = error;
    }

    public static bool TryGetMatrixError(this HttpContext context, [NotNullWhen(true)] out MatrixError? error)
    {
        ArgumentNullException.ThrowIfNull(context);

        error = null;
        if (context.Items[ItemKeys.MatrixError] is MatrixError value)
        {
            error = value;
            return true;
        }
        return false;
    }

    public static void SetAccessToken(this HttpContext context, string accessToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(accessToken);

        context.Items[ItemKeys.AccessToken] = accessToken;
    }

    public static bool TryGetAccessToken(this HttpContext context, [NotNullWhen(true)] out string? accessToken)
    {
        accessToken = null;
        if (context.Items[ItemKeys.AccessToken] is string token)
        {
            accessToken = token;
            return true;
        }
        return false;
    }

    public static string GetAccessToken(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items[ItemKeys.AccessToken] is string accessToken)
        {
            return accessToken;
        }
        else
        {
            throw new KeyNotFoundException(ItemKeys.AccessToken);
        }
    }

    public static void SetSignedRequest(this HttpContext context, SignedRequest signedRequest)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(signedRequest);

        context.Items[ItemKeys.SignedRequest] = signedRequest;
    }

    public static SignedRequest GetSignedRequest(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items[ItemKeys.SignedRequest] is SignedRequest signedRequest)
        {
            return signedRequest;
        }
        else
        {
            throw new KeyNotFoundException(ItemKeys.SignedRequest);
        }
    }
}
