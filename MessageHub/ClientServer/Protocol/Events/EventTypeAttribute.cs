namespace MessageHub.ClientServer.Protocol.Events;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
sealed class EventTypeAttribute : Attribute
{
    public string Name { get; }

    public EventTypeAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
    }
}
