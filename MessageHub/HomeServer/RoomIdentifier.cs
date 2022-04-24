using System.Diagnostics.CodeAnalysis;

namespace MessageHub.HomeServer;

public record class RoomIdentifier(string Id, string PeerId)
{
    public override string ToString()
    {
        return $"!{Id}:{PeerId}";
    }

    public static bool TryParse(string? s, [NotNullWhen(true)] out RoomIdentifier? identifier)
    {
        identifier = null;
        if (string.IsNullOrEmpty(s) || !s.StartsWith('!'))
        {
            return false;
        }
        s = s[1..];
        var parts = s.Split(':', 2);
        identifier = new RoomIdentifier(parts[0], parts[1]);
        return true;
    }

    public static RoomIdentifier Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        if (!TryParse(s, out var identifier))
        {
            throw new InvalidOperationException();
        }
        return identifier;
    }
}
