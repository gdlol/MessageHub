using Microsoft.IdentityModel.Tokens;

namespace MessageHub.HomeServer.Formatting;

public static class UnpaddedBase64Encoder
{
    public static string Encode(byte[] inArray)
    {
        ArgumentNullException.ThrowIfNull(inArray);

        string result = Base64UrlEncoder.Encode(inArray);
        return result.Replace('-', '+').Replace('_', '/');
    }

    public static string Encode(string arg)
    {
        ArgumentNullException.ThrowIfNull(arg);

        string result = Base64UrlEncoder.Encode(arg);
        return result.Replace('-', '+').Replace('_', '/');
    }

    public static string Decode(string arg)
    {
        ArgumentNullException.ThrowIfNull(arg);

        arg = arg.Replace('+', '-').Replace('/', '_');
        return Base64UrlEncoder.Decode(arg);
    }

    public static byte[] DecodeBytes(string arg)
    {
        ArgumentNullException.ThrowIfNull(arg);

        arg = arg.Replace('+', '-').Replace('/', '_');
        return Base64UrlEncoder.DecodeBytes(arg);
    }
}
