namespace MessageHub.HomeServer.Services;

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
        void handler(object? sender, T e)
        {
            Task.Run(async () =>
            {
                try
                {
                    await RunAsync(e, stoppingToken);
                }
                catch (Exception ex)
                {
                    OnError(ex);
                }
            }, stoppingToken);
        }
        notifier.OnNotify += handler;
        try
        {
            var tcs = new TaskCompletionSource();
            using var _ = stoppingToken.Register(tcs.SetResult);
            await tcs.Task;
        }
        finally
        {
            notifier.OnNotify -= handler;
        }
    }
}
