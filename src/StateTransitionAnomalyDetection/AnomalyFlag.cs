namespace StateTransitionAnomalyDetection;

public sealed record AnomalyFlag(
    string SystemName,
    string EntityType,
    string EntityId,
    string State,
    DateTimeOffset EnteredStateAt,
    TimeSpan Elapsed,
    TimeSpan ExpectedMedian,
    double Score,
    AnomalySeverity Severity);
