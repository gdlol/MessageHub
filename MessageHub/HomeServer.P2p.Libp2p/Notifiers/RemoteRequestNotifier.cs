using System.Text.Json;

namespace MessageHub.HomeServer.P2p.Libp2p.Notifiers;

public record RemoteRequest(string Destination, JsonElement Request);

public class RemoteRequestNotifier : Notifier<RemoteRequest> { }
