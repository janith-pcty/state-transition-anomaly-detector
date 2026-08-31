using StateTransitionAnomalyDetection;
using StateTransitionAnomalyDetection.Adapters.Mocks;
using StateTransitionAnomalyDetection.Host.Contracts;
using StateTransitionAnomalyDetection.Host.Services;

namespace StateTransitionAnomalyDetection.Host.Endpoints;

public static class AnomalyEndpoints
{
    public static void MapAnomalyEndpoints(this WebApplication app)
    {
        app.MapGet("/systems", async (IEnumerable<IStateTransitionSource> sources, CancellationToken ct) =>
        {
            var result = new List<SystemSummaryResponse>();
            foreach (var source in sources)
            {
                var entityTypes = await source.GetEntityTypesAsync(ct);
                result.Add(new SystemSummaryResponse(source.SystemName, entityTypes));
            }

            return Results.Ok(result);
        });

        app.MapGet("/anomalies", async (
            IEnumerable<IStateTransitionSource> sources,
            StateDurationBaselineCalculator calculator,
            AnomalyDetector detector,
            string? systemName,
            string? entityType,
            bool includeAll = false,
            CancellationToken ct = default) =>
        {
            var now = DateTimeOffset.UtcNow;
            var matchingSources = sources.Where(s => systemName is null || s.SystemName == systemName);

            var allFlags = new List<AnomalyResponse>();
            foreach (var source in matchingSources)
            {
                var entityTypes = await source.GetEntityTypesAsync(ct);
                var matchingEntityTypes = entityTypes.Where(t => entityType is null || t == entityType);

                foreach (var type in matchingEntityTypes)
                {
                    var history = await source.GetHistoryAsync(type, ct);
                    var openEntities = await source.GetOpenEntitiesAsync(type, ct);
                    var terminalStates = await source.GetTerminalStatesAsync(type, ct);

                    var baselines = calculator.Calculate(type, history, terminalStates);
                    var flags = detector.Detect(source.SystemName, type, openEntities, baselines, terminalStates, now, includeAll);

                    allFlags.AddRange(flags.Select(f => new AnomalyResponse(
                        f.SystemName,
                        f.EntityType,
                        f.EntityId,
                        f.State,
                        f.EnteredStateAt,
                        f.Elapsed.TotalSeconds,
                        f.ExpectedMedian.TotalSeconds,
                        f.Score,
                        f.Severity.ToString())));
                }
            }

            return Results.Ok(allFlags.OrderByDescending(f => f.Score).ToList());
        });

        app.MapGet("/anomalies/explain", async (
            IEnumerable<IStateTransitionSource> sources,
            StateDurationBaselineCalculator calculator,
            AnomalyDetector detector,
            IAnomalyExplainer explainer,
            CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var allFlags = new List<AnomalyFlag>();
            foreach (var source in sources)
            {
                var entityTypes = await source.GetEntityTypesAsync(ct);
                foreach (var type in entityTypes)
                {
                    var history = await source.GetHistoryAsync(type, ct);
                    var openEntities = await source.GetOpenEntitiesAsync(type, ct);
                    var terminalStates = await source.GetTerminalStatesAsync(type, ct);

                    var baselines = calculator.Calculate(type, history, terminalStates);
                    var flags = detector.Detect(source.SystemName, type, openEntities, baselines, terminalStates, now, includeAll: true);
                    allFlags.AddRange(flags);
                }
            }

            var explanation = await explainer.ExplainSystemsAsync(allFlags, ct);
            return Results.Ok(new ExplainResponse(explanation));
        });

        app.MapGet("/entities/{systemName}/{entityType}/{entityId}/history", async (
            IEnumerable<IStateTransitionSource> sources,
            StateDurationBaselineCalculator calculator,
            string systemName,
            string entityType,
            string entityId,
            CancellationToken ct) =>
        {
            var source = sources.FirstOrDefault(s => s.SystemName == systemName);
            if (source is null)
            {
                return Results.NotFound();
            }

            var history = await source.GetHistoryAsync(entityType, ct);
            var terminalStates = await source.GetTerminalStatesAsync(entityType, ct);

            var transitions = history
                .Where(e => e.EntityId == entityId)
                .OrderBy(e => e.OccurredAt)
                .Select(e => new TransitionDto(e.FromState, e.ToState, e.OccurredAt))
                .ToList();

            if (transitions.Count == 0)
            {
                return Results.NotFound();
            }

            var baselines = calculator.Calculate(entityType, history, terminalStates);
            var baselineDtos = baselines.Values
                .Select(b => new BaselineDto(
                    b.State,
                    b.Median.TotalSeconds,
                    b.SampleCount,
                    b.Samples.Select(s => s.TotalSeconds).ToList()))
                .ToList();

            return Results.Ok(new EntityHistoryResponse(entityId, entityType, transitions, baselineDtos));
        });

        app.MapPost("/reseed", (IEnumerable<IStateTransitionSource> sources) =>
        {
            foreach (var source in sources)
            {
                if (source is IReseedableSource reseedable)
                {
                    reseedable.Reseed();
                }
            }

            return Results.NoContent();
        });

        app.MapGet("/entities/{systemName}/{entityType}/states", async (
            IEnumerable<IStateTransitionSource> sources,
            string systemName,
            string entityType,
            CancellationToken ct) =>
        {
            var source = sources.FirstOrDefault(s => s.SystemName == systemName);
            if (source is null)
            {
                return Results.NotFound();
            }

            var states = await source.GetAllStatesAsync(entityType, ct);
            return Results.Ok(states);
        });

        app.MapPost("/entities/{systemName}/{entityType}", (
            IEnumerable<IStateTransitionSource> sources,
            string systemName,
            string entityType,
            CreateEntityRequest request) =>
        {
            var source = sources.FirstOrDefault(s => s.SystemName == systemName);
            if (source is not ICreatableSource creatable)
            {
                return Results.NotFound();
            }

            var (outcome, entity) = creatable.CreateEntity(entityType, request.EntityId, request.InitialState);
            return outcome switch
            {
                CreateOutcome.Success => Results.Created(
                    $"/entities/{systemName}/{entityType}/{entity!.EntityId}/history",
                    new CreateEntityResponse(entity.EntityId, entity.EntityType, entity.CurrentState, entity.EnteredStateAt)),
                CreateOutcome.UnknownEntityType => Results.NotFound(),
                CreateOutcome.InvalidState => Results.BadRequest($"'{request.InitialState}' is not a valid initial state for {entityType}."),
                CreateOutcome.DuplicateEntityId => Results.Conflict($"An entity with id '{request.EntityId}' already exists for {entityType}."),
                _ => Results.Problem(),
            };
        });

        app.MapPost("/entities/{systemName}/{entityType}/{entityId}/transition", (
            IEnumerable<IStateTransitionSource> sources,
            string systemName,
            string entityType,
            string entityId,
            TransitionRequest request) =>
        {
            var source = sources.FirstOrDefault(s => s.SystemName == systemName);
            if (source is not IManuallyTransitionableSource transitionable)
            {
                return Results.NotFound();
            }

            var outcome = transitionable.TransitionEntity(entityType, entityId, request.ToState);
            return outcome switch
            {
                TransitionOutcome.Success => Results.NoContent(),
                TransitionOutcome.EntityNotFound => Results.NotFound(),
                TransitionOutcome.InvalidState => Results.BadRequest($"'{request.ToState}' is not a valid state for {entityType}."),
                _ => Results.Problem(),
            };
        });

        app.MapGet("/entities/{systemName}/{entityType}/{entityId}/explain", async (
            IEnumerable<IStateTransitionSource> sources,
            StateDurationBaselineCalculator calculator,
            AnomalyDetector detector,
            IAnomalyExplainer explainer,
            string systemName,
            string entityType,
            string entityId,
            CancellationToken ct) =>
        {
            var source = sources.FirstOrDefault(s => s.SystemName == systemName);
            if (source is null)
            {
                return Results.NotFound();
            }

            var history = await source.GetHistoryAsync(entityType, ct);
            var openEntities = await source.GetOpenEntitiesAsync(entityType, ct);
            var terminalStates = await source.GetTerminalStatesAsync(entityType, ct);
            var baselines = calculator.Calculate(entityType, history, terminalStates);
            var flags = detector.Detect(systemName, entityType, openEntities, baselines, terminalStates, DateTimeOffset.UtcNow, includeAll: true);

            var flag = flags.FirstOrDefault(f => f.EntityId == entityId);
            if (flag is null)
            {
                return Results.NotFound();
            }

            var explanation = await explainer.ExplainAsync(flag, ct);
            return Results.Ok(new ExplainResponse(explanation));
        });
    }
}
