namespace MessageHub.HomeServer.P2p.Notifiers;

public record TopicMemberUpdate(string Topic, string Id);

public class TopicMemberUpdateNotifier : Notifier<TopicMemberUpdate> { }
