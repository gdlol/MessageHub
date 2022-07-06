using System.Collections.Immutable;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.P2p.Rooms;

using RoomCreators = ImmutableDictionary<string, string>;
using StrippedStates = ImmutableDictionary<string, ImmutableList<StrippedStateEvent>>;

internal record class EventStoreState
{
    public RoomCreators RoomCreators { get; init; } = RoomCreators.Empty;
    public ImmutableList<string> JoinedRoomIds { get; init; } = ImmutableList<string>.Empty;
    public ImmutableList<string> LeftRoomIds { get; init; } = ImmutableList<string>.Empty;
    public StrippedStates Invites { get; init; } = StrippedStates.Empty;
    public StrippedStates Knocks { get; init; } = StrippedStates.Empty;
    public string CurrentBatchId { get; init; } = string.Empty;
}
