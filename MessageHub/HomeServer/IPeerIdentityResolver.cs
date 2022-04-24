using System.Collections.Immutable;

namespace MessageHub.HomeServer;

public interface IPeerIdentityResolver
{
    ValueTask<IPeerIdentity> ResolveAsync(string peerId, long timestamp);
}
