namespace StateTransitionAnomalyDetection;

public interface IStateTransitionSource
{
    string SystemName { get; }

    Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken ct);

    Task<IReadOnlyList<StateTransitionEvent>> GetHistoryAsync(string entityType, CancellationToken ct);

    Task<IReadOnlyList<OpenEntityState>> GetOpenEntitiesAsync(string entityType, CancellationToken ct);

    Task<IReadOnlySet<string>> GetTerminalStatesAsync(string entityType, CancellationToken ct);

    Task<IReadOnlyList<string>> GetAllStatesAsync(string entityType, CancellationToken ct);
}
