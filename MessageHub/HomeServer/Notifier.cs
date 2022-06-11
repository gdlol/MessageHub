namespace MessageHub.HomeServer;

public sealed class NotifierRegistration : IDisposable
{
    private readonly Action cancel;

    internal NotifierRegistration(Action cancel)
    {
        this.cancel = cancel;
    }

    public void Dispose() => cancel();
}

public class Notifier
{
    private event EventHandler? OnNotify;

    public void Notify()
    {
        OnNotify?.Invoke(this, EventArgs.Empty);
    }

    public NotifierRegistration Register(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        void onNotify(object? sender, EventArgs e) => handler();
        OnNotify += onNotify;
        return new NotifierRegistration(() => OnNotify -= onNotify);
    }
}

public class Notifier<T>
{
    private event EventHandler<T>? OnNotify;

    public void Notify(T value)
    {
        OnNotify?.Invoke(this, value);
    }

    public NotifierRegistration Register(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        void onNotify(object? sender, T e) => handler(e);
        OnNotify += onNotify;
        return new NotifierRegistration(() => OnNotify -= onNotify);
    }
}
