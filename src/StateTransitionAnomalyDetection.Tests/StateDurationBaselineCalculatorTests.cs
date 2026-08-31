using StateTransitionAnomalyDetection;

namespace StateTransitionAnomalyDetection.Tests;

public class StateDurationBaselineCalculatorTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> NoTerminalStates = [];

    private static StateTransitionEvent Event(string entityId, string? fromState, string toState, DateTimeOffset occurredAt) =>
        new(EntityType: "Widget", EntityId: entityId, FromState: fromState, ToState: toState, OccurredAt: occurredAt);

    // Builds a two-hop chain for one entity: enters `state` at t=0, leaves it after `durationMinutes`.
    private static List<StateTransitionEvent> SingleStateChain(string entityId, string state, double durationMinutes) =>
    [
        Event(entityId, "__start__", state, BaseTime),
        Event(entityId, state, "Next", BaseTime.AddMinutes(durationMinutes)),
    ];

    [Fact]
    public void Calculate_HandVerifiedMedianAndMad()
    {
        var events = new List<StateTransitionEvent>();
        double[] durations = [4, 5, 5, 6, 30];
        for (var i = 0; i < durations.Length; i++)
        {
            events.AddRange(SingleStateChain($"E{i}", "X", durations[i]));
        }

        var calculator = new StateDurationBaselineCalculator();
        var result = calculator.Calculate("Widget", events, NoTerminalStates);

        var baseline = result["X"];
        Assert.Equal(TimeSpan.FromMinutes(5), baseline.Median);
        Assert.Equal(TimeSpan.FromMinutes(1), baseline.Mad);
        Assert.Equal(5, baseline.SampleCount);
        Assert.False(baseline.IsLowConfidence);
    }

    [Fact]
    public void Calculate_ExcludesTerminalStatesEvenWhenSamplesExist()
    {
        // Bad test data: an event recorded after the terminal "Completed" state, which would
        // otherwise produce a duration sample under "Completed".
        var events = new List<StateTransitionEvent>
        {
            Event("E1", "__start__", "Requested", BaseTime),
            Event("E1", "Requested", "Completed", BaseTime.AddMinutes(5)),
            Event("E1", "Completed", "Reopened", BaseTime.AddMinutes(10)),
        };

        var calculator = new StateDurationBaselineCalculator();
        var result = calculator.Calculate("Widget", events, new HashSet<string> { "Completed" });

        Assert.False(result.ContainsKey("Completed"));
        Assert.True(result.ContainsKey("Requested"));
    }

    [Fact]
    public void Calculate_LowSampleCountTriggersLowConfidence()
    {
        var events = new List<StateTransitionEvent>();
        double[] durations = [4, 5, 6];
        for (var i = 0; i < durations.Length; i++)
        {
            events.AddRange(SingleStateChain($"E{i}", "X", durations[i]));
        }

        var calculator = new StateDurationBaselineCalculator();
        var result = calculator.Calculate("Widget", events, NoTerminalStates);

        var baseline = result["X"];
        Assert.Equal(3, baseline.SampleCount);
        Assert.True(baseline.IsLowConfidence);
    }

    [Fact]
    public void Calculate_ZeroMadTriggersLowConfidence()
    {
        var events = new List<StateTransitionEvent>();
        for (var i = 0; i < 5; i++)
        {
            events.AddRange(SingleStateChain($"E{i}", "X", 10));
        }

        var calculator = new StateDurationBaselineCalculator();
        var result = calculator.Calculate("Widget", events, NoTerminalStates);

        var baseline = result["X"];
        Assert.Equal(TimeSpan.FromMinutes(10), baseline.Median);
        Assert.Equal(TimeSpan.Zero, baseline.Mad);
        Assert.True(baseline.IsLowConfidence);
    }

    [Fact]
    public void Calculate_GroupsDurationsAcrossEntitiesForSameState()
    {
        var events = new List<StateTransitionEvent>();
        events.AddRange(SingleStateChain("E1", "Y", 3));
        events.AddRange(SingleStateChain("E2", "Y", 9));

        var calculator = new StateDurationBaselineCalculator();
        var result = calculator.Calculate("Widget", events, NoTerminalStates);

        var baseline = result["Y"];
        Assert.Equal(2, baseline.SampleCount);
        Assert.Equal(TimeSpan.FromMinutes(6), baseline.Median);
    }
}
