using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.General;
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
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IUserPresence userPresence;
    private readonly IUserReadReceipts userReadReceipts;
    private readonly IRooms rooms;
    private readonly IEventSaver eventSaver;
    private readonly UnresolvedEventNotifier unresolvedEventNotifier;
    private readonly TimelineUpdateNotifier timelineUpdateNotifier;

    public EventReceiver(
        ILogger<EventReceiver> logger,
        IIdentityService identityService,
        IRooms rooms,
        IUserPresence userPresence,
        IUserReadReceipts userReadReceipts,
        IEventSaver eventSaver,
        UnresolvedEventNotifier unresolvedEventNotifier,
        TimelineUpdateNotifier timelineUpdateNotifier)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(userPresence);
        ArgumentNullException.ThrowIfNull(userReadReceipts);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(unresolvedEventNotifier);
        ArgumentNullException.ThrowIfNull(timelineUpdateNotifier);

        this.logger = logger;
        this.identityService = identityService;
        this.userPresence = userPresence;
        this.userReadReceipts = userReadReceipts;
        this.rooms = rooms;
        this.eventSaver = eventSaver;
        this.unresolvedEventNotifier = unresolvedEventNotifier;
        this.timelineUpdateNotifier = timelineUpdateNotifier;
    }

    public Task ReceiveEphemeralEventsAsync(UserIdentifier sender, EphemeralDataUnit[] edus)
    {
        var identity = identityService.GetSelfIdentity();
        if (identity.Id == sender.Id)
        {
            return Task.CompletedTask;
        }
        bool notifyTimelineUpdate = false;
        foreach (var edu in edus)
        {
            try
            {
                if (edu.EventType == PresenceEvent.EventType)
                {
                    var presenceUpdate = edu.Content.Deserialize<PresenceUpdate>();
                    UserPresenceUpdate? userPresenceUpdate = null;
                    if (presenceUpdate is not null)
                    {
                        foreach (var update in presenceUpdate.Push)
                        {
                            if (update.UserId != sender.ToString())
                            {
                                logger.LogInformation("Presence update not matching sender {}: {}", sender, update);
                                continue;
                            }
                            userPresenceUpdate = update;
                        }
                    }
                    if (userPresenceUpdate is not null)
                    {
                        userPresence.SetPresence(
                            sender.ToString(),
                            userPresenceUpdate.Presence,
                            userPresenceUpdate.StatusMessage);
                        notifyTimelineUpdate = true;
                    }
                }
                else if (edu.EventType == ReceiptEvent.EventType)
                {
                    var receipt = ReceiptEvent.FromEdu(edu);
                    foreach (var (roomId, roomReceipts) in receipt.Content)
                    {
                        foreach (var (userId, userReadReceipt) in roomReceipts.ReadReceipts)
                        {
                            if (userId != sender.ToString())
                            {
                                logger.LogInformation("Receipt userId not matching sender {}: {}", sender, userId);
                                continue;
                            }
                            userReadReceipts.PutReceipt(roomId, userId, ReceiptTypes.Read, userReadReceipt);
                            notifyTimelineUpdate = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation("Error receiving edu {}: {}", edu, ex.Message);
            }
        }
        if (notifyTimelineUpdate)
        {
            timelineUpdateNotifier.Notify();
        }
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
