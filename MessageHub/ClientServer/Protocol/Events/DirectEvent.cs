namespace MessageHub.ClientServer.Protocol.Events;

[EventType(EventType)]
public class DirectEvent : Dictionary<string, string[]>
{
    public const string EventType = "m.direct";
}
