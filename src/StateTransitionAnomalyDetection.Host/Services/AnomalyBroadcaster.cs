using Microsoft.AspNetCore.SignalR;
using StateTransitionAnomalyDetection;
using StateTransitionAnomalyDetection.Host.Hubs;

namespace StateTransitionAnomalyDetection.Host.Services;

public sealed class AnomalyBroadcaster
{
    private readonly IEnumerable<IStateTransitionSource> _sources;
    private readonly StateDurationBaselineCalculator _calculator;
    private readonly AnomalyDetector _detector;
    private readonly IHubContext<AnomalyHub> _hub;

    public AnomalyBroadcaster(
        IEnumerable<IStateTransitionSource> sources,
        StateDurationBaselineCalculator calculator,
        AnomalyDetector detector,
        IHubContext<AnomalyHub> hub)
    {
        _sources = sources;
        _calculator = calculator;
        _detector = detector;
        _hub = hub;
    }

    public async Task BroadcastAsync(string systemName, CancellationToken ct)
    {
        var source = _sources.FirstOrDefault(s => s.SystemName == systemName);
        if (source is null)
        {
            return;
        }

        var snapshot = await AnomalySnapshotBuilder.BuildAsync(source, _calculator, _detector, DateTimeOffset.UtcNow, ct);
        await _hub.Clients.Group(AnomalyHub.GroupName(systemName)).SendAsync("anomaliesUpdated", snapshot, ct);
    }
}
