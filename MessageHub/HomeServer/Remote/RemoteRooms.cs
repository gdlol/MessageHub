using System.Text.Json;
using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.WebUtilities;

namespace MessageHub.HomeServer.Remote;

public class RemoteRooms : IRemoteRooms
{
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IRequestHandler requestHandler;
    private readonly IEventSaver eventSaver;
    private readonly IEventReceiver eventReceiver;

    public RemoteRooms(
        ILogger<RemoteRooms> logger,
        IIdentityService identityService,
        IRequestHandler requestHandler,
        IEventSaver eventSaver,
        IEventReceiver eventReceiver)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(requestHandler);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(eventReceiver);

        this.logger = logger;
        this.identityService = identityService;
        this.requestHandler = requestHandler;
        this.eventSaver = eventSaver;
        this.eventReceiver = eventReceiver;
    }

    public Task InviteAsync(string roomId, string eventId, InviteParameters parameters)
    {
        var userId = UserIdentifier.Parse(parameters.Event.StateKey!);
        var request = identityService.GetSelfIdentity().SignRequest(
            destination: userId.Id,
            requestMethod: HttpMethods.Put,
            requestTarget: $"/_matrix/federation/v2/invite/{roomId}/{eventId}",
            content: parameters);
        return requestHandler.SendRequest(request);
    }

    public async Task<PersistentDataUnit> MakeJoinAsync(string destination, string roomId, string userId)
    {
        var request = identityService.GetSelfIdentity().SignRequest(
            destination: destination,
            requestMethod: HttpMethods.Get,
            requestTarget: $"/_matrix/federation/v1/make_join/{roomId}/{userId}");
        var result = await requestHandler.SendRequest(request);
        return JsonSerializer.Deserialize<PersistentDataUnit>(result)!;
    }

    public Task SendJoinAsync(string destination, string roomId, string eventId, JsonElement pdu)
    {
        var request = identityService.GetSelfIdentity().SignRequest(
            destination: destination,
            requestMethod: HttpMethods.Put,
            requestTarget: $"/_matrix/federation/v1/send_join/{roomId}/{eventId}",
            content: pdu);
        return requestHandler.SendRequest(request);
    }

    public async Task BackfillAsync(string destination, string roomId)
    {
        logger.LogInformation("Backfill {}...", roomId);
        async Task<PersistentDataUnit[]> backfillEvents(string[] eventIds)
        {
            string target = $"/_matrix/federation/v1/backfill/{roomId}";
            target = QueryHelpers.AddQueryString(target, "limit", 100.ToString());
            foreach (string eventId in eventIds)
            {
                target = QueryHelpers.AddQueryString(target, "v", eventId);
            }
            var request = identityService.GetSelfIdentity().SignRequest(
                destination: destination,
                requestMethod: HttpMethods.Get,
                requestTarget: target);
            var result = await requestHandler.SendRequest(request);
            return result.GetProperty("pdus").Deserialize<PersistentDataUnit[]>()!;
        }
        var pdus = new List<PersistentDataUnit>();
        var receivedEventIds = new HashSet<string>();
        var latestEventIds = Array.Empty<string>();
        while (pdus.Count == 0 || latestEventIds.Length > 0)
        {
            var newPdus = await backfillEvents(latestEventIds);
            pdus.AddRange(newPdus);
            receivedEventIds.UnionWith(newPdus.Select(EventHash.GetEventId));
            latestEventIds = pdus
                .SelectMany(x => x.PreviousEvents.Union(x.AuthorizationEvents))
                .Except(receivedEventIds)
                .ToArray();
        }
        var pduMap = pdus.ToDictionary(pdu => EventHash.GetEventId(pdu), pdu => pdu);
        var createPdu = pdus.SingleOrDefault(x => (x.EventType, x.StateKey) == (EventTypes.Create, string.Empty));
        if (createPdu is null)
        {
            logger.LogError("Create event not found");
            return;
        }
        else
        {
            string eventId = EventHash.GetEventId(createPdu);
            await eventSaver.SaveAsync(
                createPdu.RoomId,
                eventId,
                createPdu,
                new Dictionary<RoomStateKey, string>
                {
                    [new RoomStateKey(EventTypes.Create, string.Empty)] = eventId
                });
        }
        var errors = await eventReceiver.ReceivePersistentEventsAsync(pdus.ToArray());
        foreach (var (eventId, error) in errors)
        {
            if (error is not null)
            {
                var pdu = pduMap[eventId];
                logger.LogWarning("Error receiving event {eventId}: {error}, {pdu}", eventId, error, pdu);
            }
        }
    }
}
