using System.Diagnostics.CodeAnalysis;

namespace MessageHub.HomeServer;

public record class UserIdentifier(string UserName, string PeerId)
{
    public override string ToString()
    {
        return $"@{UserName}:{PeerId}";
    }

    public static bool TryParse(string? s, [NotNullWhen(true)] out UserIdentifier? identifier)
    {
        identifier = null;
        if (string.IsNullOrEmpty(s) || !s.StartsWith('@'))
        {
            return false;
        }
        s = s[1..];
        var parts = s.Split(':', 2);
        identifier = new UserIdentifier(parts[0], parts[1]);
        return true;
    }

    public static UserIdentifier Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        if (!TryParse(s, out var identifier))
        {
            throw new InvalidOperationException();
        }
        return identifier;
    }

    public static UserIdentifier FromId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new UserIdentifier(id[..Math.Min(7, id.Length)], id);
    }
}