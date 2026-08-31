namespace StateTransitionAnomalyDetection;

public sealed class StateDurationBaselineCalculator
{
    public const int MinimumSampleSize = 5;

    public IReadOnlyDictionary<string, StateDurationBaseline> Calculate(
        string entityType,
        IReadOnlyList<StateTransitionEvent> events,
        IReadOnlySet<string> terminalStates)
    {
        var durationsByState = new Dictionary<string, List<TimeSpan>>();

        var byEntity = events.GroupBy(e => e.EntityId);
        foreach (var entityEvents in byEntity)
        {
            var ordered = entityEvents.OrderBy(e => e.OccurredAt).ToList();
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                var current = ordered[i];
                var next = ordered[i + 1];
                var duration = next.OccurredAt - current.OccurredAt;

                if (!durationsByState.TryGetValue(current.ToState, out var list))
                {
                    list = [];
                    durationsByState[current.ToState] = list;
                }

                list.Add(duration);
            }
        }

        var result = new Dictionary<string, StateDurationBaseline>();
        foreach (var (state, durations) in durationsByState)
        {
            if (terminalStates.Contains(state))
            {
                continue;
            }

            var median = Median(durations);
            var mad = Median(durations.Select(d => TimeSpan.FromTicks(Math.Abs(d.Ticks - median.Ticks))).ToList());
            var isLowConfidence = durations.Count < MinimumSampleSize || mad == TimeSpan.Zero;

            result[state] = new StateDurationBaseline(
                EntityType: entityType,
                State: state,
                Median: median,
                Mad: mad,
                SampleCount: durations.Count,
                IsLowConfidence: isLowConfidence,
                Samples: durations);
        }

        return result;
    }

    private static TimeSpan Median(IReadOnlyList<TimeSpan> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;

        return sorted.Count % 2 == 0
            ? TimeSpan.FromTicks((sorted[mid - 1].Ticks + sorted[mid].Ticks) / 2)
            : sorted[mid];
    }
}
