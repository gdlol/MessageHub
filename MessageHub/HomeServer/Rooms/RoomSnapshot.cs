using System.Collections.Immutable;
using System.Text.Json;

namespace MessageHub.HomeServer.Rooms;

public class RoomSnapshot
{
    public ImmutableList<string> LatestEventIds { get; init; }
    public long GraphDepth { get; init; }
    public ImmutableDictionary<RoomStateKey, string> States { get; init; }
    public ImmutableDictionary<RoomStateKey, JsonElement> StateContents { get; init; }

    public RoomSnapshot()
    {
        LatestEventIds = ImmutableList<string>.Empty;
        GraphDepth = 0;
        States = ImmutableDictionary<RoomStateKey, string>.Empty;
        StateContents = ImmutableDictionary<RoomStateKey, JsonElement>.Empty;
    }
}
