namespace MessageHub.HomeServer.Services;

public abstract class BackgroundService
{
    private readonly object locker = new();
    private CancellationTokenSource? cts;

    // Fallback, not expected to be called.
    protected abstract void OnError(Exception error);

    protected abstract Task Start(CancellationToken stoppingToken);

    public void Start()
    {
        lock (locker)
        {
            if (cts is not null)
            {
                return;
            }
            cts = new CancellationTokenSource();
        }
        Task.Run(async () =>
        {
            try
            {
                await Start(cts.Token);
            }
            catch (OperationCanceledException ex)
            {
                if (!cts.IsCancellationRequested)
                {
                    OnError(ex);
                }
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        });
    }

    public void Stop()
    {
        var cts = this.cts;
        lock (locker)
        {
            if (cts is null)
            {
                return;
            }
        }
        try
        {
            using var _ = cts;
            cts.Cancel();
        }
        finally
        {
            this.cts = null;
        }
    }

    private class AggregatedService : BackgroundService
    {
        private readonly BackgroundService[] services;

        public AggregatedService(BackgroundService[] services)
        {
            ArgumentNullException.ThrowIfNull(services);

            this.services = services;
        }

        protected override void OnError(Exception error)
        {
            foreach (var service in services)
            {
                service.OnError(error);
            }
        }

        protected override async Task Start(CancellationToken stoppingToken)
        {
            var tcs = new TaskCompletionSource();
            using var _ = stoppingToken.Register(tcs.SetResult);
            foreach (var service in services)
            {
                service.Start();
            }
            await tcs.Task;
            foreach (var service in services.Reverse())
            {
                service.Stop();
            }
        }
    }

    public static BackgroundService Aggregate(params BackgroundService[] services)
    {
        return new AggregatedService(services);
    }
}
