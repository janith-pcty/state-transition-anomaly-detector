using StateTransitionAnomalyDetection;

namespace StateTransitionAnomalyDetection.Host.Services;

public sealed class AnomalyBroadcastService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IEnumerable<IStateTransitionSource> _sources;
    private readonly AnomalyBroadcaster _broadcaster;

    public AnomalyBroadcastService(IEnumerable<IStateTransitionSource> sources, AnomalyBroadcaster broadcaster)
    {
        _sources = sources;
        _broadcaster = broadcaster;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var source in _sources)
            {
                await _broadcaster.BroadcastAsync(source.SystemName, stoppingToken);
            }
        }
    }
}
