using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Rooms;

namespace MessageHub.HomeServer;

public class EventReceiver : IEventReceiver
{
    private readonly IPeerIdentity identity;
    private readonly IPeerStore peerStore;
    private readonly IRooms rooms;
    private readonly IEventSaver eventSaver;

    public EventReceiver(
        IPeerIdentity identity,
        IPeerStore peerStore,
        IRooms rooms,
        IEventSaver eventSaver)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(eventSaver);

        this.identity = identity;
        this.peerStore = peerStore;
        this.rooms = rooms;
        this.eventSaver = eventSaver;
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
            var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
            var roomReceiver = new RoomEventsReceiver(roomId, identity, peerStore, roomEventStore, eventSaver);
            var roomErrors = await roomReceiver.ReceiveEvents(pduList.ToArray());
            foreach (var (eventId, error) in roomErrors)
            {
                errors[eventId] = error;
            }
        }
        return errors;
    }
}
