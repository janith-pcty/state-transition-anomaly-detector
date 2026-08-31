using System.Collections.Concurrent;
using System.Diagnostics;
using StateTransitionAnomalyDetection;

namespace StateTransitionAnomalyDetection.Host.Services;

/// <summary>
/// Narrates an already-computed AnomalyFlag via the local Claude Code CLI in headless
/// print mode, authenticated through the developer's existing Claude subscription
/// session rather than a separate ANTHROPIC_API_KEY. Demo/local-dev only: requires the
/// `claude` CLI installed and logged in on the machine running this Host.
/// </summary>
public sealed class ClaudeCliAnomalyExplainer : IAnomalyExplainer
{
    private const string SystemPrompt =
        "You are a monitoring assistant. Explain anomalies in one or two plain-English " +
        "sentences and suggest a likely cause. Never restate the raw numbers verbatim, " +
        "never mention that you are an AI, and never add a preamble.";

    private readonly ConcurrentDictionary<string, string> _cache = new();

    public async Task<string> ExplainAsync(AnomalyFlag flag, CancellationToken ct)
    {
        // Keyed by entity + state rather than the live score: score drifts every second as
        // elapsed time grows, which would defeat caching entirely if included verbatim. A new
        // state (including re-entering the same state after a manual transition or reseed)
        // still gets a fresh explanation.
        var cacheKey = $"{flag.EntityId}:{flag.State}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "claude",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add("haiku");
        startInfo.ArgumentList.Add("--safe-mode");
        startInfo.ArgumentList.Add("--output-format");
        startInfo.ArgumentList.Add("text");
        startInfo.ArgumentList.Add("--system-prompt");
        startInfo.ArgumentList.Add(SystemPrompt);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        await process.StandardInput.WriteAsync(BuildPrompt(flag));
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeoutCts.Token);

        var stdout = (await stdoutTask).Trim();
        if (process.ExitCode != 0 || stdout.Length == 0)
        {
            var stderr = await stderrTask;
            throw new InvalidOperationException($"claude CLI failed (exit {process.ExitCode}): {stderr}");
        }

        _cache[cacheKey] = stdout;
        return stdout;
    }

    private static string BuildPrompt(AnomalyFlag flag) => $"""
        A monitoring system flagged this entity as anomalous:
        System: {flag.SystemName}
        Entity: {flag.EntityType} {flag.EntityId}
        Current state: {flag.State}
        Time in this state: {flag.Elapsed}
        Typical (median) time in this state: {flag.ExpectedMedian}
        Anomaly score: {flag.Score:F1} (severity: {flag.Severity})

        In one or two plain-English sentences, explain why this is anomalous and
        suggest a likely cause. Do not restate the numbers verbatim.
        """;
}
