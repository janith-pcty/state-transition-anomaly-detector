using StateTransitionAnomalyDetection;
using StateTransitionAnomalyDetection.Host.Contracts;

namespace StateTransitionAnomalyDetection.Host.Services;

public static class AnomalySnapshotBuilder
{
    public static async Task<List<AnomalyResponse>> BuildAsync(
        IStateTransitionSource source,
        StateDurationBaselineCalculator calculator,
        AnomalyDetector detector,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var flags = new List<AnomalyResponse>();
        var entityTypes = await source.GetEntityTypesAsync(ct);

        foreach (var type in entityTypes)
        {
            var history = await source.GetHistoryAsync(type, ct);
            var openEntities = await source.GetOpenEntitiesAsync(type, ct);
            var terminalStates = await source.GetTerminalStatesAsync(type, ct);

            var baselines = calculator.Calculate(type, history, terminalStates);
            var detected = detector.Detect(source.SystemName, type, openEntities, baselines, terminalStates, now, includeAll: true);

            flags.AddRange(detected.Select(f => new AnomalyResponse(
                f.SystemName,
                f.EntityType,
                f.EntityId,
                f.State,
                f.EnteredStateAt,
                f.Elapsed.TotalSeconds,
                f.ExpectedMedian.TotalSeconds,
                f.Score,
                f.Severity.ToString())));
        }

        return flags.OrderByDescending(f => f.Score).ToList();
    }
}
