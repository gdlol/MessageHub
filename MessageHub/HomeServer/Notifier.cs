namespace MessageHub.HomeServer;

public class Notifier<T>
{
    public event EventHandler<T>? OnNotify;

    public void Notify(T message)
    {
        OnNotify?.Invoke(this, message);
    }
}
