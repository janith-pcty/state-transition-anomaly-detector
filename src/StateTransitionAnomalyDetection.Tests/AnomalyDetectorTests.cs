using StateTransitionAnomalyDetection;

namespace StateTransitionAnomalyDetection.Tests;

public class AnomalyDetectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> NoTerminalStates = [];

    private static readonly StateDurationBaseline HighConfidenceBaseline = new(
        EntityType: "Widget", State: "X", Median: TimeSpan.FromMinutes(5), Mad: TimeSpan.FromMinutes(1),
        SampleCount: 5, IsLowConfidence: false, Samples: []);

    private static readonly StateDurationBaseline LowConfidenceBaseline = new(
        EntityType: "Widget", State: "X", Median: TimeSpan.FromMinutes(10), Mad: TimeSpan.Zero,
        SampleCount: 3, IsLowConfidence: true, Samples: []);

    private static OpenEntityState Entity(string id, string state, double elapsedMinutes) =>
        new(EntityType: "Widget", EntityId: id, CurrentState: state, EnteredStateAt: Now.AddMinutes(-elapsedMinutes));

    private static IReadOnlyList<AnomalyFlag> DetectWith(
        StateDurationBaseline baseline,
        OpenEntityState entity,
        bool includeAll = false,
        IReadOnlySet<string>? terminalStates = null)
    {
        var detector = new AnomalyDetector();
        var baselines = new Dictionary<string, StateDurationBaseline> { [baseline.State] = baseline };
        return detector.Detect("System", "Widget", [entity], baselines, terminalStates ?? NoTerminalStates, Now, includeAll);
    }

    [Fact]
    public void Detect_CriticalViaZScore()
    {
        var flags = DetectWith(HighConfidenceBaseline, Entity("E1", "X", 20));

        var flag = Assert.Single(flags);
        Assert.Equal(AnomalySeverity.Critical, flag.Severity);
        Assert.True(flag.Score >= 5.0);
    }

    [Fact]
    public void Detect_WarningViaZScore()
    {
        var flags = DetectWith(HighConfidenceBaseline, Entity("E1", "X", 10));

        var flag = Assert.Single(flags);
        Assert.Equal(AnomalySeverity.Warning, flag.Severity);
        Assert.InRange(flag.Score, 3.0, 5.0);
    }

    [Fact]
    public void Detect_NormalViaZScore_ExcludedByDefaultIncludedWithIncludeAll()
    {
        var noFlags = DetectWith(HighConfidenceBaseline, Entity("E1", "X", 6), includeAll: false);
        Assert.Empty(noFlags);

        var allFlags = DetectWith(HighConfidenceBaseline, Entity("E1", "X", 6), includeAll: true);
        var flag = Assert.Single(allFlags);
        Assert.Equal(AnomalySeverity.Normal, flag.Severity);
    }

    [Fact]
    public void Detect_LowConfidenceFallback_JustBelowWarningIsNormal()
    {
        var flags = DetectWith(LowConfidenceBaseline, Entity("E1", "X", 89), includeAll: true);

        var flag = Assert.Single(flags);
        Assert.Equal(AnomalySeverity.Normal, flag.Severity);
    }

    [Fact]
    public void Detect_LowConfidenceFallback_ExactlyThreeIsWarning()
    {
        var flags = DetectWith(LowConfidenceBaseline, Entity("E1", "X", 90));

        var flag = Assert.Single(flags);
        Assert.Equal(AnomalySeverity.Warning, flag.Severity);
        Assert.Equal(3.0, flag.Score, precision: 6);
    }

    [Fact]
    public void Detect_LowConfidenceFallback_ExactlyFiveIsCritical()
    {
        var flags = DetectWith(LowConfidenceBaseline, Entity("E1", "X", 150));

        var flag = Assert.Single(flags);
        Assert.Equal(AnomalySeverity.Critical, flag.Severity);
        Assert.Equal(5.0, flag.Score, precision: 6);
    }

    [Fact]
    public void Detect_SkipsEntityInTerminalCurrentState_EvenWithIncludeAll()
    {
        var flags = DetectWith(
            HighConfidenceBaseline,
            Entity("E1", "X", 20),
            includeAll: true,
            terminalStates: new HashSet<string> { "X" });

        Assert.Empty(flags);
    }

    [Fact]
    public void Detect_SkipsEntityWithNoBaseline_DoesNotThrow()
    {
        var detector = new AnomalyDetector();
        var baselines = new Dictionary<string, StateDurationBaseline>();
        var entity = Entity("E1", "NeverObserved", 20);

        var flags = detector.Detect("System", "Widget", [entity], baselines, NoTerminalStates, Now, includeAll: true);

        Assert.Empty(flags);
    }

    [Fact]
    public void Detect_SortsByScoreDescending()
    {
        var detector = new AnomalyDetector();
        var baselines = new Dictionary<string, StateDurationBaseline> { ["X"] = HighConfidenceBaseline };
        var entities = new List<OpenEntityState>
        {
            Entity("Low", "X", 10),
            Entity("High", "X", 30),
            Entity("Mid", "X", 20),
        };

        var flags = detector.Detect("System", "Widget", entities, baselines, NoTerminalStates, Now);

        Assert.Equal(["High", "Mid", "Low"], flags.Select(f => f.EntityId));
    }
}
