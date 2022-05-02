using System.Text.Json;
using System.Web;
using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Remote;

public class RemoteRooms : IRemoteRooms
{
    private readonly IPeerIdentity peerIdentity;
    private readonly IRequestHandler requestHandler;
    private readonly IEventReceiver eventReceiver;

    public RemoteRooms(IPeerIdentity peerIdentity, IRequestHandler requestHandler, IEventReceiver eventReceiver)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(requestHandler);
        ArgumentNullException.ThrowIfNull(eventReceiver);

        this.peerIdentity = peerIdentity;
        this.requestHandler = requestHandler;
        this.eventReceiver = eventReceiver;
    }

    public Task InviteAsync(string roomId, string eventId, InviteParameters parameters)
    {
        var userId = UserIdentifier.Parse(parameters.Event.StateKey!);
        var request = peerIdentity.SignRequest(
            destination: userId.PeerId,
            requestMethod: HttpMethods.Put,
            requestTarget: $"_matrix/federation/v2/invite/{roomId}/{eventId}",
            content: parameters);
        return requestHandler.SendRequest(request);
    }

    public async Task<PersistentDataUnit> MakeJoinAsync(string roomId, string userId)
    {
        var request = peerIdentity.SignRequest(
            destination: roomId,
            requestMethod: HttpMethods.Get,
            requestTarget: $"_matrix/federation/v1/make_join/{roomId}/{userId}");
        var result = await requestHandler.SendRequest(request);
        return JsonSerializer.Deserialize<PersistentDataUnit>(result)!;
    }

    public Task SendJoinAsync(string roomId, string eventId, JsonElement pdu)
    {
        var request = peerIdentity.SignRequest(
            destination: roomId,
            requestMethod: HttpMethods.Put,
            requestTarget: $"_matrix/federation/v1/send_join/{roomId}/{eventId}",
            content: pdu);
        return requestHandler.SendRequest(request);
    }

    public async Task BackfillAsync(string roomId)
    {
        async Task<PersistentDataUnit[]> backfillEvents(string[] eventIds)
        {
            var uriBuilder = new UriBuilder($"_matrix/federation/v1/backfill/{roomId}");
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            foreach (string eventId in eventIds)
            {
                query.Add("v", eventId);
            }
            uriBuilder.Query = query.ToString();
            var request = peerIdentity.SignRequest(
                destination: roomId,
                requestMethod: HttpMethods.Get,
                requestTarget: $"_matrix/federation/v1/backfill/{roomId}");
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
            latestEventIds = pdus.SelectMany(x => x.PreviousEvents).Except(receivedEventIds).ToArray();
        }
        var errors = await eventReceiver.ReceivePersistentEventsAsync(pdus.ToArray());
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(JsonSerializer.Serialize(errors, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
    }
}
