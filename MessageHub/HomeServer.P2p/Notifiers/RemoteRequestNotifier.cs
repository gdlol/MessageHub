using System.Text.Json;

namespace MessageHub.HomeServer.P2p.Notifiers;

public record RemoteRequest(string Destination, JsonElement Request);

public class RemoteRequestNotifier : Notifier<RemoteRequest> { }
