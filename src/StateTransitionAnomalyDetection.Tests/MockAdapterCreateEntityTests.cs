using StateTransitionAnomalyDetection.Adapters.Mocks;

namespace StateTransitionAnomalyDetection.Tests;

public class MockAdapterCreateEntityTests
{
    public sealed record AdapterCase(
        string EntityType, string DefaultState, string TerminalState, string OtherState, Func<ICreatableSource> Factory);

    public static IEnumerable<object[]> Adapters()
    {
        yield return [new AdapterCase("PrintJob", "Requested", "Completed", "Queued", () => new PrintJobMockAdapter())];
        yield return [new AdapterCase("Ticket", "New", "Resolved", "Triaged", () => new SupportTicketMockAdapter())];
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void CreateEntity_Defaults_UsesFirstStateAndGeneratedId(AdapterCase testCase)
    {
        var source = testCase.Factory();

        var (outcome, entity) = source.CreateEntity(testCase.EntityType, null, null);

        Assert.Equal(CreateOutcome.Success, outcome);
        Assert.NotNull(entity);
        Assert.Equal(testCase.DefaultState, entity!.CurrentState);
        Assert.StartsWith("MANUAL-", entity.EntityId);
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void CreateEntity_Defaults_GeneratesUniqueIds(AdapterCase testCase)
    {
        var source = testCase.Factory();

        var (_, first) = source.CreateEntity(testCase.EntityType, null, null);
        var (_, second) = source.CreateEntity(testCase.EntityType, null, null);

        Assert.NotEqual(first!.EntityId, second!.EntityId);
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void CreateEntity_ExplicitInitialState_UsesProvidedState(AdapterCase testCase)
    {
        var source = testCase.Factory();

        var (outcome, entity) = source.CreateEntity(testCase.EntityType, null, testCase.OtherState);

        Assert.Equal(CreateOutcome.Success, outcome);
        Assert.Equal(testCase.OtherState, entity!.CurrentState);
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void CreateEntity_ExplicitEntityId_UsesProvidedId(AdapterCase testCase)
    {
        var source = testCase.Factory();

        var (outcome, entity) = source.CreateEntity(testCase.EntityType, "custom-id-1", null);

        Assert.Equal(CreateOutcome.Success, outcome);
        Assert.Equal("custom-id-1", entity!.EntityId);
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void CreateEntity_DuplicateEntityId_ReturnsDuplicateOutcome(AdapterCase testCase)
    {
        var source = testCase.Factory();
        source.CreateEntity(testCase.EntityType, "dup-id", null);

        var (outcome, entity) = source.CreateEntity(testCase.EntityType, "dup-id", null);

        Assert.Equal(CreateOutcome.DuplicateEntityId, outcome);
        Assert.Null(entity);
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void CreateEntity_UnknownEntityType_ReturnsUnknownEntityType(AdapterCase testCase)
    {
        var source = testCase.Factory();

        var (outcome, entity) = source.CreateEntity("NotARealType", null, null);

        Assert.Equal(CreateOutcome.UnknownEntityType, outcome);
        Assert.Null(entity);
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void CreateEntity_TerminalInitialState_ReturnsInvalidState(AdapterCase testCase)
    {
        var source = testCase.Factory();

        var (outcome, entity) = source.CreateEntity(testCase.EntityType, null, testCase.TerminalState);

        Assert.Equal(CreateOutcome.InvalidState, outcome);
        Assert.Null(entity);
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void CreateEntity_UnrecognizedInitialState_ReturnsInvalidState(AdapterCase testCase)
    {
        var source = testCase.Factory();

        var (outcome, entity) = source.CreateEntity(testCase.EntityType, null, "NotARealState");

        Assert.Equal(CreateOutcome.InvalidState, outcome);
        Assert.Null(entity);
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void CreateEntity_WhitespaceOnlyValues_TreatedAsOmitted(AdapterCase testCase)
    {
        var source = testCase.Factory();

        var (outcome, entity) = source.CreateEntity(testCase.EntityType, "   ", "   ");

        Assert.Equal(CreateOutcome.Success, outcome);
        Assert.Equal(testCase.DefaultState, entity!.CurrentState);
        Assert.StartsWith("MANUAL-", entity.EntityId);
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public async Task CreateEntity_Success_AppearsInOpenEntitiesAndHistory(AdapterCase testCase)
    {
        var source = testCase.Factory();
        var stateSource = (StateTransitionAnomalyDetection.IStateTransitionSource)source;

        var (outcome, _) = source.CreateEntity(testCase.EntityType, "visible-id", testCase.OtherState);
        Assert.Equal(CreateOutcome.Success, outcome);

        var openEntities = await stateSource.GetOpenEntitiesAsync(testCase.EntityType, CancellationToken.None);
        Assert.Contains(openEntities, e => e.EntityId == "visible-id" && e.CurrentState == testCase.OtherState);

        var history = await stateSource.GetHistoryAsync(testCase.EntityType, CancellationToken.None);
        var newEvent = Assert.Single(history, e => e.EntityId == "visible-id");
        Assert.Null(newEvent.FromState);
        Assert.Equal(testCase.OtherState, newEvent.ToState);
    }
}
