using System.Text.Json;

namespace MessageHub.HomeServer.P2p.Notifiers;

public class RemoteRequestNotifier : Notifier<(string, JsonElement)> { }
