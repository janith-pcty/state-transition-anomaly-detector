namespace StateTransitionAnomalyDetection;

public sealed class AnomalyDetector
{
    private const double MadScaleFactor = 1.4826;
    private const double FallbackMedianMultiplier = 3.0;
    private const double WarningThreshold = 3.0;
    private const double CriticalThreshold = 5.0;

    public IReadOnlyList<AnomalyFlag> Detect(
        string systemName,
        string entityType,
        IReadOnlyList<OpenEntityState> openEntities,
        IReadOnlyDictionary<string, StateDurationBaseline> baselines,
        IReadOnlySet<string> terminalStates,
        DateTimeOffset now,
        bool includeAll = false)
    {
        var flags = new List<AnomalyFlag>();

        foreach (var entity in openEntities)
        {
            if (terminalStates.Contains(entity.CurrentState))
            {
                continue;
            }

            if (!baselines.TryGetValue(entity.CurrentState, out var baseline))
            {
                continue;
            }

            var elapsed = now - entity.EnteredStateAt;

            var score = baseline.IsLowConfidence
                ? elapsed.TotalSeconds / (FallbackMedianMultiplier * baseline.Median.TotalSeconds)
                : (elapsed.TotalSeconds - baseline.Median.TotalSeconds) / (MadScaleFactor * baseline.Mad.TotalSeconds);

            var severity = score switch
            {
                >= CriticalThreshold => AnomalySeverity.Critical,
                >= WarningThreshold => AnomalySeverity.Warning,
                _ => AnomalySeverity.Normal,
            };

            if (severity == AnomalySeverity.Normal && !includeAll)
            {
                continue;
            }

            flags.Add(new AnomalyFlag(
                SystemName: systemName,
                EntityType: entityType,
                EntityId: entity.EntityId,
                State: entity.CurrentState,
                EnteredStateAt: entity.EnteredStateAt,
                Elapsed: elapsed,
                ExpectedMedian: baseline.Median,
                Score: score,
                Severity: severity));
        }

        return flags.OrderByDescending(f => f.Score).ToList();
    }
}
