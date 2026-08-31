using StateTransitionAnomalyDetection;

namespace StateTransitionAnomalyDetection.Host.Services;

public interface IAnomalyExplainer
{
    Task<string> ExplainAsync(AnomalyFlag flag, CancellationToken ct);

    Task<string> ExplainSystemsAsync(IReadOnlyList<AnomalyFlag> flags, CancellationToken ct);
}
