using System.Collections.Concurrent;

namespace MessageHub.HomeServer.Services;

public abstract class QueuedService<T> : BackgroundService
{
    private readonly Notifier<T> notifier;
    private readonly int boundedCapacity;
    private readonly int maxDegreeOfParallelism;

    protected QueuedService(Notifier<T> notifier, int boundedCapacity, int maxDegreeOfParallelism = 1)
    {
        ArgumentNullException.ThrowIfNull(notifier);

        this.notifier = notifier;
        this.boundedCapacity = boundedCapacity;
        this.maxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    protected abstract Task RunAsync(T value, CancellationToken stoppingToken);

    protected override async Task Start(CancellationToken stoppingToken)
    {
        using var queue = new BlockingCollection<T>(boundedCapacity);
        void handler(object? sender, T e) => queue.TryAdd(e);
        notifier.OnNotify += handler;
        try
        {
            using var _ = stoppingToken.Register(queue.CompleteAdding);
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = stoppingToken,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };
            await Parallel.ForEachAsync(
                queue.GetConsumingEnumerable(stoppingToken),
                parallelOptions,
                async (value, token) =>
                {
                    try
                    {
                        await RunAsync(value, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        OnError(ex);
                    }
                });
        }
        finally
        {
            notifier.OnNotify -= handler;
        }
    }
}
