using DeployFlow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DeployFlow.Application.Services;

/// <summary>
/// DAG-based pipeline execution engine.
///
/// Upgrade over the simple sequential/parallel runner:
///   • Stages are represented as a Directed Acyclic Graph via <see cref="PipelineStage.DependsOn"/> (comma-separated stage names).
///   • Topological sort ensures dependencies are respected.
///   • Stages with the SAME topological level are dispatched in parallel.
///   • Per-step retry with exponential back-off (<see cref="PipelineStep.RetryCount"/>).
///   • Circuit-breaker: if a step fails more than <see cref="CircuitBreakerThreshold"/> consecutive
///     times in a run, that stage is hard-failed without using remaining retry budget.
///   • Total run timeout guard (<see cref="MaxRunDurationMinutes"/>).
/// </summary>
public sealed class PipelineDagEngine
{
    private readonly ILogger _logger;

    // ── Tunables (can be moved to appsettings later) ─────────────────────────
    private const int    CircuitBreakerThreshold = 3;
    private const double MaxRunDurationMinutes   = 60.0;
    private const int    RetryBaseDelayMs        = 2_000;

    public PipelineDagEngine(ILogger logger)
    {
        _logger = logger;
    }

    // ── Main entry point ─────────────────────────────────────────────────────

    /// <summary>
    /// Execute pipeline stages in DAG order.
    ///
    /// Returns <c>true</c> when all stages succeed, <c>false</c> on any failure.
    /// </summary>
    public async Task<DagRunResult> ExecuteAsync(
        IList<PipelineStage>                          stages,
        Func<PipelineStage, PipelineStep, Task<bool>> stepExecutor,
        Func<string, string, string, Task>            logWriter,    // (stageName, stepName?, message)
        CancellationToken                             ct = default)
    {
        var result     = new DagRunResult();
        var runStart   = DateTime.UtcNow;
        var levelGroups = TopologicalLevels(stages);

        using var runTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        runTimeout.CancelAfter(TimeSpan.FromMinutes(MaxRunDurationMinutes));

        foreach (var group in levelGroups)
        {
            if (DateTime.UtcNow - runStart > TimeSpan.FromMinutes(MaxRunDurationMinutes))
            {
                await logWriter("pipeline", "", "⏱ Run timeout exceeded — aborting");
                result.TimedOut = true;
                return result;
            }

            // All stages in the same level can run in parallel
            var stageTasks = group.Select(stage =>
                RunStageAsync(stage, stepExecutor, logWriter, result, runTimeout.Token));

            var stageResults = await Task.WhenAll(stageTasks);

            if (stageResults.Any(r => !r))
            {
                // Mark all remaining stages as skipped
                var executedNames = new HashSet<string>(group.Select(s => s.Name));
                var remaining     = levelGroups
                    .SkipWhile(g => !g.Contains(group[0]))
                    .Skip(1)
                    .SelectMany(g => g)
                    .Where(s => !executedNames.Contains(s.Name));

                foreach (var skipped in remaining)
                    await logWriter(skipped.Name, "", "⏭️ Stage skipped (prior failure)");

                return result;
            }
        }

        result.Succeeded = true;
        return result;
    }

    // ── Stage runner ─────────────────────────────────────────────────────────

    private async Task<bool> RunStageAsync(
        PipelineStage                                 stage,
        Func<PipelineStage, PipelineStep, Task<bool>> stepExecutor,
        Func<string, string, string, Task>            logWriter,
        DagRunResult                                  result,
        CancellationToken                             ct)
    {
        await logWriter(stage.Name, "", $"▶  Stage: {stage.Name}");

        if (stage.RunParallel)
        {
            var tasks = stage.Steps
                .OrderBy(s => s.Id)
                .Select(step => RunStepWithRetryAsync(stage, step, stepExecutor, logWriter, ct))
                .ToList();

            var results = await Task.WhenAll(tasks);
            bool ok = results.All(r => r);
            result.StageOutcomes[stage.Name] = ok;

            await logWriter(stage.Name, "", ok
                ? $"✅ Stage '{stage.Name}' completed (parallel)"
                : $"❌ Stage '{stage.Name}' failed (parallel)");
            return ok;
        }
        else
        {
            int consecutiveFails = 0;
            foreach (var step in stage.Steps.OrderBy(s => s.Id))
            {
                if (consecutiveFails >= CircuitBreakerThreshold)
                {
                    await logWriter(stage.Name, step.Name,
                        $"⚡ Circuit breaker tripped after {consecutiveFails} consecutive failures — skipping remaining steps");
                    result.StageOutcomes[stage.Name] = false;
                    return false;
                }

                bool stepOk = await RunStepWithRetryAsync(stage, step, stepExecutor, logWriter, ct);
                if (!stepOk)
                {
                    consecutiveFails++;
                    result.StageOutcomes[stage.Name] = false;

                    if (!stage.ContinueOnFailure)
                    {
                        await logWriter(stage.Name, "", $"❌ Stage '{stage.Name}' failed at step '{step.Name}'");
                        return false;
                    }

                    await logWriter(stage.Name, step.Name,
                        "⚠️ Step failed but ContinueOnFailure=true — continuing");
                }
                else
                {
                    consecutiveFails = 0;
                }
            }

            bool stageOk = !result.StageOutcomes.ContainsKey(stage.Name) || result.StageOutcomes[stage.Name];
            result.StageOutcomes[stage.Name] = stageOk;
            await logWriter(stage.Name, "", stageOk
                ? $"✅ Stage '{stage.Name}' completed"
                : $"❌ Stage '{stage.Name}' completed with failures");
            return stageOk;
        }
    }

    // ── Step retry with exponential back-off ─────────────────────────────────

    private async Task<bool> RunStepWithRetryAsync(
        PipelineStage                                 stage,
        PipelineStep                                  step,
        Func<PipelineStage, PipelineStep, Task<bool>> stepExecutor,
        Func<string, string, string, Task>            logWriter,
        CancellationToken                             ct)
    {
        int maxAttempts = Math.Max(1, (step.RetryCount ?? 0) + 1);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                int delayMs = RetryBaseDelayMs * (int)Math.Pow(2, attempt - 2); // 2s, 4s, 8s…
                await logWriter(stage.Name, step.Name, $"  🔁 Retry {attempt - 1}/{maxAttempts - 1} in {delayMs}ms…");
                await Task.Delay(delayMs, ct);
            }

            try
            {
                bool ok = await stepExecutor(stage, step);
                if (ok) return true;

                if (attempt < maxAttempts)
                    await logWriter(stage.Name, step.Name, $"  ⚠️  Attempt {attempt} failed — will retry");
                else
                    await logWriter(stage.Name, step.Name, $"  ❌ Step '{step.Name}' exhausted {maxAttempts} attempts");
            }
            catch (OperationCanceledException)
            {
                await logWriter(stage.Name, step.Name, "  🛑 Step cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("DAG: step {Step} attempt {Attempt} threw: {Msg}", step.Name, attempt, ex.Message);
                if (attempt == maxAttempts)
                    await logWriter(stage.Name, step.Name, $"  💥 Step threw: {ex.Message}");
            }
        }

        return false;
    }

    // ── Topological sort → parallel level groups ──────────────────────────────

    /// <summary>
    /// Kahn's algorithm: produces groups of stages that can be executed in parallel.
    /// Stages without explicit DependsOn are placed at level 0 (run first).
    /// </summary>
    public static List<List<PipelineStage>> TopologicalLevels(IList<PipelineStage> stages)
    {
        var byName     = stages.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var inDegree   = stages.ToDictionary(s => s.Name, _ => 0, StringComparer.OrdinalIgnoreCase);
        var dependents = stages.ToDictionary(s => s.Name, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var stage in stages)
        {
            foreach (var dep in GetDependencies(stage))
            {
                if (byName.ContainsKey(dep))
                {
                    inDegree[stage.Name]++;
                    dependents[dep].Add(stage.Name);
                }
            }
        }

        // Fall back to Order-based linear execution if no DependsOn is set
        bool hasDependencies = stages.Any(s => !string.IsNullOrWhiteSpace(s.DependsOn));
        if (!hasDependencies)
        {
            // Respect existing Order field — one stage per level
            return stages
                .OrderBy(s => s.Order)
                .Select(s => new List<PipelineStage> { s })
                .ToList();
        }

        // Kahn's BFS
        var levels = new List<List<PipelineStage>>();
        var queue  = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        while (queue.Count > 0)
        {
            var level     = new List<PipelineStage>();
            int cnt       = queue.Count;

            for (int i = 0; i < cnt; i++)
            {
                var name  = queue.Dequeue();
                level.Add(byName[name]);

                foreach (var dep in dependents[name])
                {
                    if (--inDegree[dep] == 0)
                        queue.Enqueue(dep);
                }
            }

            levels.Add(level);
        }

        // Any remaining stages have a cycle — append them linearly as a safety net
        var processed = new HashSet<string>(levels.SelectMany(l => l.Select(s => s.Name)), StringComparer.OrdinalIgnoreCase);
        var cyclic    = stages.Where(s => !processed.Contains(s.Name)).ToList();
        if (cyclic.Count > 0)
            levels.Add(cyclic);

        return levels;
    }

    private static IEnumerable<string> GetDependencies(PipelineStage stage)
    {
        if (string.IsNullOrWhiteSpace(stage.DependsOn))
            return Enumerable.Empty<string>();

        return stage.DependsOn
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

// ── Result model ─────────────────────────────────────────────────────────────

public sealed class DagRunResult
{
    public bool                       Succeeded    { get; set; }
    public bool                       TimedOut     { get; set; }
    public Dictionary<string, bool>   StageOutcomes { get; } = new(StringComparer.OrdinalIgnoreCase);
}
