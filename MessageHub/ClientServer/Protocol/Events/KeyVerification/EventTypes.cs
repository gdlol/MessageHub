namespace MessageHub.ClientServer.Protocol.Events.KeyVerification;

public static class EventTypes
{
    public const string Request = "m.key.verification.request";
    public const string Ready = "m.key.verification.ready";
    public const string Start = "m.key.verification.start";
    public const string Accept = "m.key.verification.accept";
    public const string Key = "m.key.verification.key";
    public const string Mac = "m.key.verification.mac";
    public const string Done = "m.key.verification.done";
    public const string Cancel = "m.key.verification.cancel";
}
