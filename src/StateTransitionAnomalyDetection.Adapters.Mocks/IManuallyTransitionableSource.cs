namespace StateTransitionAnomalyDetection.Adapters.Mocks;

public enum TransitionOutcome
{
    Success,
    EntityNotFound,
    InvalidState,
}

/// <summary>
/// Demo-only capability: force an open entity into a different state, for driving a live demo.
/// Kept out of the core IStateTransitionSource contract since real sources wouldn't support this.
/// </summary>
public interface IManuallyTransitionableSource
{
    TransitionOutcome TransitionEntity(string entityType, string entityId, string toState);
}
