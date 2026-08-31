namespace StateTransitionAnomalyDetection;

public sealed record OpenEntityState(
    string EntityType,
    string EntityId,
    string CurrentState,
    DateTimeOffset EnteredStateAt);
