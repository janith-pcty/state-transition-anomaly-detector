namespace StateTransitionAnomalyDetection.Adapters.Mocks;

public enum CreateOutcome
{
    Success,
    UnknownEntityType,
    InvalidState,
    DuplicateEntityId,
}

/// <summary>
/// Demo-only capability: create a brand-new open entity so it can be flagged, transitioned, or
/// reseeded like any synthetic one, for driving a live demo.
/// Kept out of the core IStateTransitionSource contract since real sources wouldn't support this.
/// </summary>
public interface ICreatableSource
{
    (CreateOutcome Outcome, OpenEntityState? Entity) CreateEntity(string entityType, string? entityId, string? initialState);
}
