namespace StateTransitionAnomalyDetection;

public sealed record StateTransitionEvent(
    string EntityType,
    string EntityId,
    string? FromState,
    string ToState,
    DateTimeOffset OccurredAt);
