using Microsoft.AspNetCore.SignalR;
using StateTransitionAnomalyDetection;
using StateTransitionAnomalyDetection.Host.Services;

namespace StateTransitionAnomalyDetection.Host.Hubs;

public sealed class AnomalyHub : Hub
{
    public static string GroupName(string systemName) => $"system:{systemName}";

    private readonly IEnumerable<IStateTransitionSource> _sources;
    private readonly StateDurationBaselineCalculator _calculator;
    private readonly AnomalyDetector _detector;

    public AnomalyHub(
        IEnumerable<IStateTransitionSource> sources,
        StateDurationBaselineCalculator calculator,
        AnomalyDetector detector)
    {
        _sources = sources;
        _calculator = calculator;
        _detector = detector;
    }

    public async Task SubscribeToSystem(string systemName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(systemName));

        var source = _sources.FirstOrDefault(s => s.SystemName == systemName);
        if (source is null)
        {
            return;
        }

        var snapshot = await AnomalySnapshotBuilder.BuildAsync(source, _calculator, _detector, DateTimeOffset.UtcNow, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("anomaliesUpdated", snapshot, Context.ConnectionAborted);
    }

    public Task UnsubscribeFromSystem(string systemName) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(systemName));
}
