namespace MessageHub.ClientServerProtocol.Events.Room;

public static class RoomEventTypes
{
    public const string CanonicalAlias = "m.room.canonical_alias";
    public const string Create = "m.room.create";
    public const string JoinRules = "m.room.join_rules";
    public const string Member = "m.room.member";
    public const string PowerLevels = "m.room.power_levels";
    public const string Message = "m.room.message";
    public const string Name = "m.room.name";
    public const string Topic = "m.room.topic";
    public const string Avatar = "m.room.avatar";
    public const string PinnedEvents = "m.room.pinned_events";
}
