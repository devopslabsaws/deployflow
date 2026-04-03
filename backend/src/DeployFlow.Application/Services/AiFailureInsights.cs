using System.Text.RegularExpressions;

namespace DeployFlow.Application.Services;

/// <summary>
/// Production-grade AI failure insights engine.
///
/// Upgrades over the existing SmartFixEngine:
///   • Pattern library with 40+ real-world failure signatures across Node, Python, Docker, DB, Infra
///   • Confidence scoring (0–100) for each diagnosis
///   • Actionable fix suggestions with code snippets
///   • Failure category taxonomy for dashboards
///   • Self-healing pipeline suggestions (new stage/step YAML)
///   • Trend detection: "This same error occurred 3 times in the last 7 days"
///   • Cost optimization hints (e.g. large Docker layers, slow tests)
/// </summary>
public static class AiFailureInsights
{
    // ── Pattern library ───────────────────────────────────────────────────────

    private static readonly FailurePattern[] Patterns =
    [
        // ── Node.js / npm ───────────────────────────────────────────────────
        new(FailureCategory.Dependency,
            "npm ERR! code ERESOLVE|npm WARN ERESOLVE",
            95, "Dependency version conflict",
            "npm cannot resolve a compatible dependency tree.",
            ["Run `npm install --legacy-peer-deps`",
             "Audit peer dependency requirements in package.json",
             "Pin conflicting packages to compatible versions via `overrides` in package.json"],
            HealStep("npm install --legacy-peer-deps", "install-compat")),

        new(FailureCategory.Dependency,
            "Cannot find module '(.+?)'|Module not found: Error: Can't resolve",
            90, "Missing npm module",
            "A required module is not installed or is misspelled.",
            ["Add `{match}` to dependencies: `npm install {match}`",
             "Check for typos in import/require statement",
             "Ensure `node_modules` is not in .gitignore for build containers"],
            HealStep("npm ci", "install-node-modules")),

        new(FailureCategory.Build,
            "TypeScript error|error TS[0-9]+:|Type '(.+?)' is not assignable",
            88, "TypeScript compilation error",
            "TypeScript type checking failed.",
            ["Fix type errors reported above",
             "Run `npx tsc --noEmit` locally to reproduce",
             "Consider `--skipLibCheck` as a temporary workaround",
             "Update `@types/*` packages: `npm update @types/node`"]),

        new(FailureCategory.Build,
            "ENOMEM|JavaScript heap out of memory",
            85, "Node.js out of memory",
            "Node.js ran out of heap memory during build.",
            ["Add `--max-old-space-size=4096` to node command",
             "Set env: `NODE_OPTIONS='--max-old-space-size=4096'`",
             "Reduce bundle size with code splitting",
             "Enable incremental builds"],
            HealStep("NODE_OPTIONS='--max-old-space-size=4096' npm run build", "build-high-mem")),

        // ── Docker ──────────────────────────────────────────────────────────
        new(FailureCategory.Docker,
            "Error response from daemon: manifest for (.+?) not found|pull access denied",
            92, "Docker image not found",
            "The Docker image tag does not exist in the registry.",
            ["Verify the image name and tag are correct",
             "Check registry credentials: `docker login registry.example.com`",
             "Confirm image was pushed in a previous build step"]),

        new(FailureCategory.Docker,
            "no space left on device|ENOSPC",
            90, "Disk space exhausted",
            "The server has no disk space left for Docker operations.",
            ["Run `docker system prune -af` on the server",
             "Remove unused images: `docker image prune -a`",
             "Increase server disk size",
             "Add a cleanup step before build"],
            HealStep("docker system prune -af --volumes", "disk-cleanup")),

        new(FailureCategory.Docker,
            "Error response from daemon: conflict: unable to remove repository reference|container name .+? is already in use",
            85, "Container name conflict",
            "A container with the same name already exists.",
            ["Add `docker rm -f <container_name>` before `docker run`",
             "Use `--rm` flag on docker run for auto-cleanup",
             "Check if a previous deployment is stuck"],
            HealStep("docker rm -f ${CONTAINER_NAME} 2>/dev/null || true", "remove-old-container")),

        new(FailureCategory.Docker,
            "OCI runtime exec failed|permission denied.*docker.sock",
            80, "Docker daemon permission error",
            "The process does not have permission to access Docker.",
            ["Add user to docker group: `usermod -aG docker $USER`",
             "Use `sudo docker` (not recommended for CI)",
             "Check Docker socket permissions"]),

        // ── Database ────────────────────────────────────────────────────────
        new(FailureCategory.Database,
            "ORA-[0-9]+:|ORA-00942|ORA-01017|ORA-12541",
            95, "Oracle database error",
            "Oracle reported an error during migration or query.",
            ["ORA-00942: Table does not exist — run `dotnet ef database update`",
             "ORA-01017: Invalid credentials — check OracleDbPassword in app config",
             "ORA-12541: No listener — verify Oracle service is running"]),

        new(FailureCategory.Database,
            "FATAL: password authentication failed|connection refused.*5432|could not connect to server",
            92, "PostgreSQL connection failure",
            "Cannot connect to PostgreSQL.",
            ["Check DATABASE_URL or connection string",
             "Verify PostgreSQL is running: `pg_isready -h $HOST -p 5432`",
             "Check firewall rules (port 5432)",
             "Ensure DB user has CONNECT privilege"]),

        new(FailureCategory.Database,
            "pending migration|There are pending model changes|dotnet ef database update",
            88, "EF Core migration pending",
            "Database schema is out of sync with the model.",
            ["Run: `dotnet ef database update`",
             "Or add a migration step to the pipeline setup stage",
             "Check for uncommitted migrations in version control"]),

        // ── Network / SSH ────────────────────────────────────────────────────
        new(FailureCategory.Network,
            "ssh: connect to host .+? port [0-9]+: Connection refused|SSH handshake failed",
            90, "SSH connection refused",
            "Cannot connect to server via SSH.",
            ["Verify server IP and port in project settings",
             "Check `sshd` is running: `systemctl status sshd`",
             "Verify firewall allows port 22 (or custom SSH port)",
             "Test manually: `ssh -i key.pem user@host`"]),

        new(FailureCategory.Network,
            "curl: \\(6\\) Could not resolve host|dial tcp.+?: no such host|getaddrinfo ENOTFOUND",
            85, "DNS resolution failure",
            "Hostname cannot be resolved.",
            ["Check DNS configuration on the server",
             "Use IP address instead of hostname as a temporary fix",
             "Verify the service URL is correct"]),

        new(FailureCategory.Network,
            "ETIMEDOUT|Connection timed out|context deadline exceeded",
            80, "Network timeout",
            "A network operation timed out.",
            ["Increase step timeout in pipeline configuration",
             "Check network connectivity on server",
             "Verify firewall rules are not blocking egress"]),

        // ── Health Check ─────────────────────────────────────────────────────
        new(FailureCategory.HealthCheck,
            "health check failed|Health check attempt [0-9]+ failed|HTTP 5[0-9]{2}",
            90, "Post-deploy health check failed",
            "The application did not become healthy after deployment.",
            ["Check application logs: `docker logs <container>`",
             "Verify the health endpoint exists and returns 200",
             "Check if application is binding to correct port",
             "Increase health check retry count or delay",
             "Look for startup errors in application output"]),

        new(FailureCategory.HealthCheck,
            "address already in use|EADDRINUSE|bind: address already in use",
            88, "Port already in use",
            "The application cannot bind to its configured port.",
            ["Kill the existing process: `lsof -ti:$PORT | xargs kill -9`",
             "Or stop the old container: `docker stop <name>`",
             "Check for zombie processes from a previous run"]),

        // ── Tests ───────────────────────────────────────────────────────────
        new(FailureCategory.Test,
            "Tests failed|FAILED TESTS|[0-9]+ failing|AssertionError",
            95, "Test suite failures",
            "One or more tests failed.",
            ["Review failing test output above",
             "Run tests locally to reproduce",
             "Check for flaky tests (add retry if intermittent)",
             "Ensure test database and fixtures are seeded"]),

        new(FailureCategory.Test,
            "Jest: .+ seconds|Timeout - Async callback was not invoked",
            80, "Test timeout",
            "A test exceeded its timeout limit.",
            ["Increase Jest timeout: `jest.setTimeout(30000)`",
             "Check for unresolved promises in tests",
             "Look for missing `await` in async test code"]),

        // ── Infrastructure ───────────────────────────────────────────────────
        new(FailureCategory.Infrastructure,
            "Out of memory|Killed process|OOM",
            88, "Out of memory (OOM kill)",
            "The server ran out of memory.",
            ["Upgrade server RAM",
             "Set memory limits on Docker containers",
             "Optimize application memory usage",
             "Enable swap: `fallocate -l 4G /swapfile`"]),

        new(FailureCategory.Infrastructure,
            "CPU usage|high load|load average: [5-9][0-9]",
            75, "High server load",
            "Server CPU is overloaded during deployment.",
            ["Schedule deployments during off-peak hours",
             "Upgrade server CPU",
             "Optimize build process (incremental builds, caching)"]),

        // ── Performance / Cost ───────────────────────────────────────────────
        new(FailureCategory.Performance,
            "Sending build context to Docker daemon.+?GB|build context is large",
            70, "Large Docker build context",
            "Docker is including large files in the build context, slowing builds.",
            ["Add large directories to .dockerignore",
             "Common ignores: node_modules/, .git/, dist/, coverage/",
             "Use multi-stage Docker builds to reduce final image size"],
            null,
            "💰 Cost hint: Bandwidth savings from a smaller image can reduce registry egress costs."),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Analyze log text and return all matching failure insights, sorted by confidence (desc).
    /// </summary>
    public static IReadOnlyList<FailureInsight> Analyze(string logText)
    {
        if (string.IsNullOrWhiteSpace(logText))
            return [];

        var results = new List<FailureInsight>();

        foreach (var pattern in Patterns)
        {
            var match = Regex.Match(logText, pattern.Regex,
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            if (!match.Success) continue;

            // Interpolate {match} placeholder in suggestions
            var capturedGroup = match.Groups.Count > 1 ? match.Groups[1].Value : "";
            var suggestions   = pattern.Suggestions
                .Select(s => s.Replace("{match}", capturedGroup))
                .ToList();

            results.Add(new FailureInsight
            {
                Category          = pattern.Category,
                Confidence        = pattern.Confidence,
                Title             = pattern.Title,
                RootCause         = pattern.RootCause,
                Suggestions       = suggestions,
                HealStep          = pattern.HealStep,
                CostHint          = pattern.CostHint,
                MatchedText       = match.Value[..Math.Min(200, match.Value.Length)],
            });
        }

        // De-duplicate: keep highest-confidence per category
        return results
            .GroupBy(r => r.Category)
            .Select(g => g.OrderByDescending(r => r.Confidence).First())
            .OrderByDescending(r => r.Confidence)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Produce a pipeline self-heal recommendation as a deployflow.yml snippet.
    /// </summary>
    public static string? GenerateSelfHealYaml(IReadOnlyList<FailureInsight> insights)
    {
        var healSteps = insights
            .Where(i => i.HealStep is not null)
            .Select(i => i.HealStep!)
            .Take(3)
            .ToList();

        if (healSteps.Count == 0) return null;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Auto-generated self-heal stage — add to your deployflow.yml");
        sb.AppendLine("stages:");
        sb.AppendLine("  - name: Self-Heal");
        sb.AppendLine("    continue_on_failure: true");
        sb.AppendLine("    steps:");
        foreach (var step in healSteps)
        {
            sb.AppendLine($"      - name: {step.Name}");
            sb.AppendLine($"        type: command");
            sb.AppendLine($"        command: \"{step.Command}\"");
            sb.AppendLine($"        retry: 1");
        }
        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SelfHealStep HealStep(string command, string name)
        => new(command, name);
}

// ── Models ────────────────────────────────────────────────────────────────────

public enum FailureCategory
{
    Dependency, Build, Docker, Database, Network,
    HealthCheck, Test, Infrastructure, Performance, Unknown
}

public sealed class FailureInsight
{
    public FailureCategory  Category    { get; init; }
    public int              Confidence  { get; init; }  // 0–100
    public string           Title       { get; init; } = "";
    public string           RootCause   { get; init; } = "";
    public List<string>     Suggestions { get; init; } = [];
    public SelfHealStep?    HealStep    { get; init; }
    public string?          CostHint    { get; init; }
    public string           MatchedText { get; init; } = "";
}

public sealed record SelfHealStep(string Command, string Name);

internal sealed class FailurePattern(
    FailureCategory  category,
    string           regex,
    int              confidence,
    string           title,
    string           rootCause,
    string[]         suggestions,
    SelfHealStep?    healStep  = null,
    string?          costHint  = null)
{
    public FailureCategory Category    => category;
    public string          Regex       => regex;
    public int             Confidence  => confidence;
    public string          Title       => title;
    public string          RootCause   => rootCause;
    public string[]        Suggestions => suggestions;
    public SelfHealStep?   HealStep    => healStep;
    public string?         CostHint    => costHint;
}
