using System.Security.Cryptography;

namespace MessageHub.HomeServer.Services;

public abstract class ScheduledService : BackgroundService
{
    private readonly TimeSpan initialDelay;
    private readonly TimeSpan interval;
    private readonly double jitterRange;

    protected ScheduledService(TimeSpan initialDelay, TimeSpan interval, double jitterRange = 0.2)
    {
        if (initialDelay < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentException(initialDelay.ToString(), nameof(initialDelay));
        }
        if (interval < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentException(interval.ToString(), nameof(interval));
        }
        if (jitterRange < 0 || jitterRange >= 0.8)
        {
            throw new ArgumentOutOfRangeException($"{nameof(jitterRange)}: {jitterRange}");
        }

        this.initialDelay = initialDelay;
        this.interval = interval;
        this.jitterRange = jitterRange;
    }

    private Task DelayWithJitter(TimeSpan delay, CancellationToken cancellationToken)
    {
        int range = Convert.ToInt32(jitterRange * delay.TotalMilliseconds);
        if (range > 0)
        {
            var jitter = RandomNumberGenerator.GetInt32(-range, range);
            delay += TimeSpan.FromMilliseconds(jitter);
        }
        return Task.Delay(delay, cancellationToken);
    }

    protected abstract Task RunAsync(CancellationToken stoppingToken);

    protected override async Task Start(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();
        await DelayWithJitter(initialDelay, stoppingToken);
        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException ex)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                OnError(ex);
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
            await DelayWithJitter(interval, stoppingToken);
        }
    }
}
