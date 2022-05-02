using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Remote;

public interface IRemoteRooms
{
    Task InviteAsync(string roomId, string eventId, InviteParameters parameters);
    Task<PersistentDataUnit> MakeJoinAsync(string roomId, string userId);
    Task SendJoinAsync(string roomId, string userId, JsonElement pdu);
    Task BackfillAsync(string roomId);
}
