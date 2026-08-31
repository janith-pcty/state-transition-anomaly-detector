namespace StateTransitionAnomalyDetection.Host.Contracts;

public sealed record SystemSummaryResponse(string SystemName, IReadOnlyList<string> EntityTypes);

public sealed record AnomalyResponse(
    string SystemName,
    string EntityType,
    string EntityId,
    string State,
    DateTimeOffset EnteredStateAt,
    double ElapsedSeconds,
    double ExpectedMedianSeconds,
    double Score,
    string Severity);

public sealed record TransitionDto(string? FromState, string ToState, DateTimeOffset OccurredAt);

public sealed record BaselineDto(string State, double MedianSeconds, int SampleCount, IReadOnlyList<double> DurationsSeconds);

public sealed record EntityHistoryResponse(
    string EntityId,
    string EntityType,
    IReadOnlyList<TransitionDto> Transitions,
    IReadOnlyList<BaselineDto> Baseline);

public sealed record TransitionRequest(string ToState);

public sealed record ExplainResponse(string Explanation);
