namespace MessageHub.HomeServer.Services;

public abstract class TriggeredService : BackgroundService
{
    private readonly Notifier notifier;

    protected TriggeredService(Notifier notifier)
    {
        ArgumentNullException.ThrowIfNull(notifier);

        this.notifier = notifier;
    }

    protected abstract Task RunAsync(CancellationToken stoppingToken);

    protected override async Task Start(CancellationToken stoppingToken)
    {
        void handler()
        {
            Task.Run(async () =>
            {
                try
                {
                    await RunAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    OnError(ex);
                }
            }, stoppingToken);
        }
        using var _ = notifier.Register(handler);
        var tcs = new TaskCompletionSource();
        using var __ = stoppingToken.Register(tcs.SetResult);
        await tcs.Task;
    }
}

public abstract class TriggeredService<T> : BackgroundService
{
    private readonly Notifier<T> notifier;

    protected TriggeredService(Notifier<T> notifier)
    {
        ArgumentNullException.ThrowIfNull(notifier);

        this.notifier = notifier;
    }

    protected abstract Task RunAsync(T value, CancellationToken stoppingToken);

    protected override async Task Start(CancellationToken stoppingToken)
    {
        void handler(T value)
        {
            Task.Run(async () =>
            {
                try
                {
                    await RunAsync(value, stoppingToken);
                }
                catch (Exception ex)
                {
                    OnError(ex);
                }
            }, stoppingToken);
        }
        using var _ = notifier.Register(handler);
        var tcs = new TaskCompletionSource();
        using var __ = stoppingToken.Register(tcs.SetResult);
        await tcs.Task;
    }
}
