using StateTransitionAnomalyDetection;

namespace StateTransitionAnomalyDetection.Adapters.Mocks;

/// <summary>
/// Generates synthetic state-transition data for a linear state chain (first state -> ... -> terminal state).
/// Seeded so a given run's sequence of Generate*/Reseed calls is reproducible.
/// </summary>
public sealed class SyntheticHistoryGenerator(int seed)
{
    private readonly Random _random = new(seed);

    public IReadOnlyList<StateTransitionEvent> GenerateHistoricalEntities(
        string entityType,
        IReadOnlyList<string> stateChain,
        IReadOnlyDictionary<string, (double MinMinutes, double MaxMinutes)> durationRanges,
        int count,
        DateTimeOffset now,
        string idPrefix)
    {
        var events = new List<StateTransitionEvent>();

        for (var i = 0; i < count; i++)
        {
            var entityId = $"{idPrefix}-{i + 1}";
            var currentTime = now.AddDays(-_random.Next(1, 30)).AddHours(-_random.Next(0, 24));
            string? fromState = null;

            for (var idx = 0; idx < stateChain.Count; idx++)
            {
                var state = stateChain[idx];
                events.Add(new StateTransitionEvent(entityType, entityId, fromState, state, currentTime));
                fromState = state;

                if (idx < stateChain.Count - 1)
                {
                    currentTime += RandomDuration(durationRanges[state], allowOutlier: true);
                }
            }
        }

        return events;
    }

    public (IReadOnlyList<StateTransitionEvent> Events, IReadOnlyList<OpenEntityState> OpenEntities) GenerateOpenEntities(
        string entityType,
        IReadOnlyList<string> stateChain,
        IReadOnlyDictionary<string, (double MinMinutes, double MaxMinutes)> durationRanges,
        int count,
        int pinnedStuckCount,
        DateTimeOffset now,
        string idPrefix)
    {
        var events = new List<StateTransitionEvent>();
        var openEntities = new List<OpenEntityState>();
        var pinnedIndices = new HashSet<int>(
            Enumerable.Range(0, count).OrderBy(_ => _random.Next()).Take(pinnedStuckCount));

        for (var i = 0; i < count; i++)
        {
            var entityId = $"{idPrefix}-{i + 1}";
            var openStateIndex = _random.Next(0, stateChain.Count - 1); // exclude the terminal state
            var openState = stateChain[openStateIndex];
            var range = durationRanges[openState];

            var elapsedMinutes = pinnedIndices.Contains(i)
                ? range.MaxMinutes * (4 + _random.NextDouble() * 4) // 4x-8x normal duration
                : range.MinMinutes + _random.NextDouble() * (range.MaxMinutes - range.MinMinutes);

            var enteredStateAt = now.AddMinutes(-elapsedMinutes);

            // Walk backward from the open state to the chain's start to build this entity's prior history,
            // so drill-down timelines exist for open (including flagged) entities, not just completed ones.
            var entityEvents = new List<StateTransitionEvent>();
            var time = enteredStateAt;
            for (var idx = openStateIndex; idx >= 0; idx--)
            {
                var fromState = idx == 0 ? null : stateChain[idx - 1];
                entityEvents.Add(new StateTransitionEvent(entityType, entityId, fromState, stateChain[idx], time));

                if (idx > 0)
                {
                    time -= RandomDuration(durationRanges[stateChain[idx - 1]], allowOutlier: false);
                }
            }

            entityEvents.Reverse();
            events.AddRange(entityEvents);
            openEntities.Add(new OpenEntityState(entityType, entityId, openState, enteredStateAt));
        }

        return (events, openEntities);
    }

    private TimeSpan RandomDuration((double MinMinutes, double MaxMinutes) range, bool allowOutlier)
    {
        var minutes = range.MinMinutes + _random.NextDouble() * (range.MaxMinutes - range.MinMinutes);

        if (allowOutlier && _random.NextDouble() < 0.05)
        {
            minutes *= 3 + _random.NextDouble() * 3; // 3x-6x historical outlier
        }

        return TimeSpan.FromMinutes(minutes);
    }
}
