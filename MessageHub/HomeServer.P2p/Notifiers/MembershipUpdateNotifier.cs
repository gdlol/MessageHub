namespace MessageHub.HomeServer.P2p.Notifiers;

public record MembershipUpdate(string RoomId, string[] Members);

public class MembershipUpdateNotifier : Notifier<MembershipUpdate> { }
