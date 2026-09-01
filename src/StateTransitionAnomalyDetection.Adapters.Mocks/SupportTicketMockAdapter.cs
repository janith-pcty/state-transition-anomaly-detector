using StateTransitionAnomalyDetection;

namespace StateTransitionAnomalyDetection.Adapters.Mocks;

public sealed class SupportTicketMockAdapter : IStateTransitionSource, IReseedableSource, IManuallyTransitionableSource, ICreatableSource
{
    private const string EntityTypeName = "Ticket";
    private const int HistoricalCount = 60;
    private const int OpenCount = 8;
    private const int PinnedStuckCount = 1;

    private static readonly string[] StateChain =
        ["New", "Triaged", "InProgress", "WaitingOnCustomer", "Resolved"];

    private static readonly HashSet<string> TerminalStates = ["Resolved", "Closed"];

    private static readonly IReadOnlyList<string> AllStates =
        [.. StateChain, .. TerminalStates.Except(StateChain)];

    private static readonly Dictionary<string, (double MinMinutes, double MaxMinutes)> DurationRanges = new()
    {
        ["New"] = (5, 20),
        ["Triaged"] = (15, 45),
        ["InProgress"] = (60, 240),
        ["WaitingOnCustomer"] = (30, 120),
    };

    private readonly SyntheticHistoryGenerator _generator;
    private readonly IReadOnlyList<StateTransitionEvent> _historicalEvents;
    private readonly Lock _lock = new();
    private (IReadOnlyList<StateTransitionEvent> Events, IReadOnlyList<OpenEntityState> OpenEntities) _openData;

    public SupportTicketMockAdapter(int seed = 99)
    {
        _generator = new SyntheticHistoryGenerator(seed);
        var now = DateTimeOffset.UtcNow;
        _historicalEvents = _generator.GenerateHistoricalEntities(
            EntityTypeName, StateChain, DurationRanges, HistoricalCount, now, idPrefix: "TCK");
        _openData = _generator.GenerateOpenEntities(
            EntityTypeName, StateChain, DurationRanges, OpenCount, PinnedStuckCount, now, idPrefix: "OPEN");
    }

    public string SystemName => "SupportTickets";

    public Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>([EntityTypeName]);

    public Task<IReadOnlyList<StateTransitionEvent>> GetHistoryAsync(string entityType, CancellationToken ct)
    {
        if (entityType != EntityTypeName)
        {
            return Task.FromResult<IReadOnlyList<StateTransitionEvent>>([]);
        }

        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<StateTransitionEvent>>([.. _historicalEvents, .. _openData.Events]);
        }
    }

    public Task<IReadOnlyList<OpenEntityState>> GetOpenEntitiesAsync(string entityType, CancellationToken ct)
    {
        if (entityType != EntityTypeName)
        {
            return Task.FromResult<IReadOnlyList<OpenEntityState>>([]);
        }

        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<OpenEntityState>>([.. _openData.OpenEntities]);
        }
    }

    public Task<IReadOnlySet<string>> GetTerminalStatesAsync(string entityType, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<string>>(TerminalStates);

    public Task<IReadOnlyList<string>> GetAllStatesAsync(string entityType, CancellationToken ct) =>
        Task.FromResult(AllStates);

    public void Reseed()
    {
        var newOpenData = _generator.GenerateOpenEntities(
            EntityTypeName, StateChain, DurationRanges, OpenCount, PinnedStuckCount, DateTimeOffset.UtcNow, idPrefix: "OPEN");

        lock (_lock)
        {
            _openData = newOpenData;
        }
    }

    public TransitionOutcome TransitionEntity(string entityType, string entityId, string toState)
    {
        if (entityType != EntityTypeName)
        {
            return TransitionOutcome.EntityNotFound;
        }

        if (!AllStates.Contains(toState))
        {
            return TransitionOutcome.InvalidState;
        }

        lock (_lock)
        {
            var openEntities = _openData.OpenEntities;
            var index = -1;
            for (var i = 0; i < openEntities.Count; i++)
            {
                if (openEntities[i].EntityId == entityId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return TransitionOutcome.EntityNotFound;
            }

            var entity = openEntities[index];
            var now = DateTimeOffset.UtcNow;
            var newEvents = new List<StateTransitionEvent>(_openData.Events)
            {
                new(EntityTypeName, entityId, entity.CurrentState, toState, now),
            };

            List<OpenEntityState> newOpenEntities;
            if (TerminalStates.Contains(toState))
            {
                newOpenEntities = openEntities.Where(e => e.EntityId != entityId).ToList();
            }
            else
            {
                newOpenEntities = new List<OpenEntityState>(openEntities)
                {
                    [index] = entity with { CurrentState = toState, EnteredStateAt = now },
                };
            }

            _openData = (newEvents, newOpenEntities);
        }

        return TransitionOutcome.Success;
    }

    public (CreateOutcome Outcome, OpenEntityState? Entity) CreateEntity(string entityType, string? entityId, string? initialState)
    {
        if (entityType != EntityTypeName)
        {
            return (CreateOutcome.UnknownEntityType, null);
        }

        var resolvedState = string.IsNullOrWhiteSpace(initialState) ? StateChain[0] : initialState;
        if (!AllStates.Contains(resolvedState) || TerminalStates.Contains(resolvedState))
        {
            return (CreateOutcome.InvalidState, null);
        }

        lock (_lock)
        {
            var resolvedId = string.IsNullOrWhiteSpace(entityId) ? $"MANUAL-{Guid.NewGuid():N}" : entityId;

            if (_openData.OpenEntities.Any(e => e.EntityId == resolvedId))
            {
                return (CreateOutcome.DuplicateEntityId, null);
            }

            var now = DateTimeOffset.UtcNow;
            var entity = new OpenEntityState(EntityTypeName, resolvedId, resolvedState, now);

            var newEvents = new List<StateTransitionEvent>(_openData.Events)
            {
                new(EntityTypeName, resolvedId, null, resolvedState, now),
            };
            var newOpenEntities = new List<OpenEntityState>(_openData.OpenEntities) { entity };

            _openData = (newEvents, newOpenEntities);

            return (CreateOutcome.Success, entity);
        }
    }
}
