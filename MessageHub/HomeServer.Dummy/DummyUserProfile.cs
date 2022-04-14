using System.Text.Json;
using MessageHub.ClientServerProtocol.Events.Room;

namespace MessageHub.HomeServer.Dummy;

public class DummyUserProfile : IUserProfile
{
    private string? avatarUrl;
    private string? displayName;
    private readonly DummyEventSender eventSender;

    public DummyUserProfile()
    {
        eventSender = new DummyEventSender();
    }

    public Task<string?> GetAvatarUrlAsync(string userId)
    {
        return Task.FromResult(avatarUrl);
    }

    public Task<string?> GetDisplayNameAsync(string userId)
    {
        return Task.FromResult(displayName);
    }

    public async Task SetAvatarUrlAsync(string userId, string url)
    {
        avatarUrl = url;
        var roomStates = RoomHistory.RoomStatesList[^1];
        foreach (var roomId in roomStates.JoinedRoomIds)
        {
            var room = roomStates.Rooms[roomId];
            var state = room.States[room.EventIds[^1]];
            if (state.TryGetValue(new RoomStateKey(EventTypes.Member, userId), out string? eventId))
            {
                var stateEvent = room.LoadClientEventWithoutRoomId(eventId);
                var content = stateEvent.Content.Deserialize<MemberEvent>()!;
                content.AvatarUrl = url;
                await eventSender.SendStateEventAsync(
                    userId,
                    roomId,
                    new RoomStateKey(EventTypes.Member, userId),
                    JsonSerializer.SerializeToElement(content));
            }
        }
    }

    public async Task SetDisplayNameAsync(string userId, string name)
    {
        displayName = name;
        var roomStates = RoomHistory.RoomStatesList[^1];
        foreach (var roomId in roomStates.JoinedRoomIds)
        {
            var room = roomStates.Rooms[roomId];
            var state = room.States[room.EventIds[^1]];
            if (state.TryGetValue(new RoomStateKey(EventTypes.Member, userId), out string? eventId))
            {
                var stateEvent = room.LoadClientEventWithoutRoomId(eventId);
                var content = stateEvent.Content.Deserialize<MemberEvent>()!;
                content.DisplayName = name;
                await eventSender.SendStateEventAsync(
                    userId,
                    roomId,
                    new RoomStateKey(EventTypes.Member, userId),
                    JsonSerializer.SerializeToElement(content));
            }
        }
    }
}
