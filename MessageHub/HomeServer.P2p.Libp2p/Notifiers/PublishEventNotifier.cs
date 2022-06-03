using System.Text.Json;

namespace MessageHub.HomeServer.P2p.Notifiers;

public record PublishEvent(string Topic, JsonElement Message);

public class PublishEventNotifier : Notifier<PublishEvent> { }
