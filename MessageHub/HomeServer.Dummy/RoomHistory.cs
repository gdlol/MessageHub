using System.Collections.Immutable;
using MessageHub.ClientServerProtocol;

namespace MessageHub.HomeServer.Dummy;

public class RoomHistory
{
    public ImmutableList<ClientEvent> Events { get; private set; }

    public RoomHistory()
    {
        Events = ImmutableList<ClientEvent>.Empty;
    }
}
