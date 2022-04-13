using System.Text.Json;
using MessageHub.ClientServerProtocol;

namespace MessageHub.HomeServer;

public interface IEventSender
{
    Task<(string? eventId, MatrixError? error)> SendStateEventAsync(
        string sender,
        string roomId,
        RoomStateKey stateKey,
        JsonElement content);

    Task<(string? eventId, MatrixError? error)> SendMessageEventAsync(
        string sender,
        string roomId,
        string eventType,
        string transactionId,
        JsonElement content);
}
