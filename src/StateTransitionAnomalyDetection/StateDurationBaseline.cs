namespace StateTransitionAnomalyDetection;

public sealed record StateDurationBaseline(
    string EntityType,
    string State,
    TimeSpan Median,
    TimeSpan Mad,
    int SampleCount,
    bool IsLowConfidence,
    IReadOnlyList<TimeSpan> Samples);
