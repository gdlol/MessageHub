using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer;

public static class EventReceiveErrors
{
    public const string NotResolved = "Not resolved";
    public const string Rejected = "Rejected";
    public const string InvalidEventId = "Invalid event ID";
}

public class EventReceiver : IEventReceiver
{
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;
    private readonly IEventSaver eventSaver;
    private readonly UnresolvedEventNotifier unresolvedEventNotifier;

    public EventReceiver(
        IIdentityService identityService,
        IRooms rooms,
        IEventSaver eventSaver,
        UnresolvedEventNotifier unresolvedEventNotifier)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(unresolvedEventNotifier);

        this.identityService = identityService;
        this.rooms = rooms;
        this.eventSaver = eventSaver;
        this.unresolvedEventNotifier = unresolvedEventNotifier;
    }

    public Task ReceiveEphemeralEventsAsync(EphemeralDataUnit[] edus)
    {
        return Task.CompletedTask;
    }

    public async Task<Dictionary<string, string?>> ReceivePersistentEventsAsync(PersistentDataUnit[] pdus)
    {
        var errors = new Dictionary<string, string?>();
        var roomPdus = new Dictionary<string, List<PersistentDataUnit>>();
        foreach (var pdu in pdus)
        {
            string? eventId = EventHash.TryGetEventId(pdu);
            if (eventId is null)
            {
                continue;
            }
            if (!rooms.HasRoom(pdu.RoomId))
            {
                errors[eventId] = $"{nameof(pdu.RoomId)}: {pdu.RoomId}";
            }
            if (!roomPdus.TryGetValue(pdu.RoomId, out var pduList))
            {
                pduList = roomPdus[pdu.RoomId] = new List<PersistentDataUnit>();
            }
            pduList.Add(pdu);
        }
        foreach (var (roomId, pduList) in roomPdus)
        {
            using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
            var roomReceiver = new RoomEventsReceiver(
                roomId,
                identityService,
                roomEventStore,
                eventSaver,
                unresolvedEventNotifier);
            var roomErrors = await roomReceiver.ReceiveEventsAsync(pduList.ToArray());
            var unresolvedEvents = new List<PersistentDataUnit>();
            foreach (var (eventId, error) in roomErrors)
            {
                errors[eventId] = error;
            }
        }
        return errors;
    }
}
