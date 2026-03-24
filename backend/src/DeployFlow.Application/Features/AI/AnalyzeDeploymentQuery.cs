using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using System.Text.RegularExpressions;

namespace DeployFlow.Application.Features.AI;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record AiAnalysisDto(
    string DeploymentId,
    string Severity,           // critical | high | medium | low | ok
    string Diagnosis,          // human readable summary
    string RootCause,
    List<AiSuggestion> Suggestions,
    List<AiAutoFix> AutoFixes,
    DateTime AnalyzedAt
);

public record AiSuggestion(string Title, string Detail, string Category);
public record AiAutoFix(string Id, string Label, string Command, bool Destructive);

public record AiChatMessage(string Role, string Content);  // role: user | assistant

public record AiChatRequest(string Message, string? DeploymentId);
public record AiChatResponse(string Reply, List<string> QuickReplies);

// ── Query ─────────────────────────────────────────────────────────────────────

public record AnalyzeDeploymentQuery(Guid DeploymentId) : IRequest<Result<AiAnalysisDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public class AnalyzeDeploymentQueryHandler : IRequestHandler<AnalyzeDeploymentQuery, Result<AiAnalysisDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public AnalyzeDeploymentQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<AiAnalysisDto>> Handle(AnalyzeDeploymentQuery request, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(request.DeploymentId, ct);
        if (deployment is null || deployment.TenantId != _currentUser.TenantId)
            return Result<AiAnalysisDto>.Failure("404", "Deployment not found.");

        // collect up to 500 log lines
        var (logs, _) = await _uow.Deployments.GetLogsPagedAsync(request.DeploymentId, 1, 500, ct);
        var logText   = string.Join("\n", logs.Select(l => $"[{l.Level}] {l.Message}"));
        var errors    = logs.Where(l => l.Level == LogLevel.Error).ToList();
        var errorText = string.Join("\n", errors.Select(l => l.Message));

        // ── Rule-based pattern engine ─────────────────────────────────────────
        var suggestions = new List<AiSuggestion>();
        var autoFixes   = new List<AiAutoFix>();
        string severity = "ok";
        string diagnosis, rootCause;

        // OOM (Out of Memory)
        if (Regex.IsMatch(errorText + logText, @"out of memory|OOMKilled|memory limit|exit code 137", RegexOptions.IgnoreCase))
        {
            severity  = "critical";
            diagnosis = "Container was killed due to Out-Of-Memory (OOM). The process exceeded allocated memory limits.";
            rootCause = "Memory allocation exceeds container limits. Common causes: memory leak, lack of memory limits, large dataset processing without pagination.";
            suggestions.AddRange([
                new("Increase memory limit", "Set a higher memory limit in your service configuration (e.g., 512 MiB → 1 GiB).", "resource"),
                new("Profile memory usage", "Add memory profiling (e.g., dotnet-dump, node --max-old-space-size) to identify the leak source.", "diagnostics"),
                new("Implement streaming", "For large dataset processing, switch from loading all data at once to streaming/pagination.", "code"),
            ]);
            autoFixes.Add(new("scale-memory", "Double memory limit", "scale memory 2x", false));
        }
        // Port already in use
        else if (Regex.IsMatch(errorText, @"address already in use|port.*in use|EADDRINUSE|bind: address", RegexOptions.IgnoreCase))
        {
            severity  = "high";
            diagnosis = "Port conflict detected — another process is already bound to the target port.";
            rootCause = "Previous container instance may still be running, or the host port is reserved by another service.";
            suggestions.AddRange([
                new("Stop conflicting container", "Run `docker ps` to find the container holding the port and stop it.", "infra"),
                new("Use dynamic port assignment", "Let the container runtime assign the host port dynamically to avoid conflicts.", "config"),
            ]);
            autoFixes.Add(new("force-redeploy", "Force-kill old container and redeploy", "force-stop-and-redeploy", true));
        }
        // Docker build failure
        else if (Regex.IsMatch(errorText, @"dockerfile|COPY failed|RUN.*failed|no such file or directory.*Dockerfile", RegexOptions.IgnoreCase))
        {
            severity  = "high";
            diagnosis = "Docker image build failed. Check Dockerfile paths, base image availability, and build context.";
            rootCause = "Missing files in build context, incorrect COPY paths, or unreachable base image registry.";
            suggestions.AddRange([
                new("Verify Dockerfile paths", "Ensure all COPY/ADD sources exist relative to the Docker build context.", "code"),
                new("Check base image", "Confirm the base image tag (e.g., node:18-alpine) is available on the registry.", "infra"),
                new("Add .dockerignore", "Exclude node_modules, .git, etc. to reduce context size and avoid path conflicts.", "optimization"),
            ]);
        }
        // Missing env variable
        else if (Regex.IsMatch(errorText, @"environment variable|ENV.*not set|undefined.*process\.env\.|KeyNotFoundException|is null or empty.*config", RegexOptions.IgnoreCase))
        {
            severity  = "high";
            diagnosis = "Required environment variable is not set. The application cannot start without it.";
            rootCause = "Missing or misconfigured environment variable in deployment configuration.";
            suggestions.AddRange([
                new("Set required env vars", "Go to Project → Environment Variables and add the missing variable.", "config"),
                new("Use .env validation", "Add startup validation (e.g., envalid for Node.js, IOptions validation for .NET) to fail fast on missing config.", "code"),
            ]);
            autoFixes.Add(new("open-env-editor", "Open environment variable editor", "open-env-ui", false));
        }
        // Connection refused / DB connectivity
        else if (Regex.IsMatch(errorText, @"connection refused|ECONNREFUSED|cannot connect.*database|ORA-\d{5}|connection timed out", RegexOptions.IgnoreCase))
        {
            severity  = "high";
            diagnosis = "Network connectivity failure — the application cannot reach a downstream service (database, cache, API).";
            rootCause = "Service may not be running, wrong connection string, firewall rules, or service discovery misconfiguration.";
            suggestions.AddRange([
                new("Verify connection strings", "Check all connection strings in environment variables for correct host/port/credentials.", "config"),
                new("Check downstream service health", "Verify the target service (DB, Redis, etc.) is online and accessible from this server.", "infra"),
                new("Check firewall/security groups", "Ensure no firewall rule blocks outbound traffic on the required port.", "infra"),
            ]);
        }
        // Exit code non-zero (generic build failure)
        else if (Regex.IsMatch(errorText + logText, @"exit code [1-9]\d*|exited with code|Process.*exited", RegexOptions.IgnoreCase))
        {
            severity  = "medium";
            diagnosis = "Build or startup script exited with a non-zero exit code, indicating a failure in the build pipeline.";
            rootCause = "Script failure, dependency installation error, or compilation error. Review the full build log.";
            suggestions.AddRange([
                new("Review full build log", "Scroll through the Logs tab to find the first error message before the process exit.", "diagnostics"),
                new("Test locally", "Run the same build commands locally (npm install && npm run build) to reproduce the error.", "code"),
            ]);
        }
        // Healthy deployment (no errors)
        else if (!errors.Any() && deployment.Status.ToString().ToLower() is "healthy" or "running")
        {
            severity  = "ok";
            diagnosis = "Deployment is running healthy. No error patterns detected in the logs.";
            rootCause = "No issues found.";
            suggestions.Add(new("Set up health checks", "Configure periodic health check pings so you get alerts if the service degrades.", "monitoring"));
        }
        else
        {
            // fallback for failed deployments with no matched pattern
            severity  = errors.Any() ? "medium" : "low";
            diagnosis = errors.Any()
                ? $"Found {errors.Count} error log entries. Review the specific error messages below."
                : "Deployment did not complete successfully. No specific error pattern was identified.";
            rootCause = "Unknown — additional log context needed.";
            suggestions.Add(new("Add verbose logging", "Enable debug-level logging to get more detail about the failure point.", "diagnostics"));
        }

        // Universal suggestions for failed deployments
        if (severity is "critical" or "high")
        {
            suggestions.Add(new(
                "Enable deployment notifications",
                "Configure Slack/email alerts so your team is immediately notified of critical failures.",
                "alerting"));
        }

        return Result<AiAnalysisDto>.Success(new AiAnalysisDto(
            DeploymentId:  request.DeploymentId.ToString(),
            Severity:      severity,
            Diagnosis:     diagnosis,
            RootCause:     rootCause,
            Suggestions:   suggestions,
            AutoFixes:     autoFixes,
            AnalyzedAt:    DateTime.UtcNow
        ));
    }
}

// ── Chat Query ────────────────────────────────────────────────────────────────

public record AiDeploymentChatCommand(string Message, Guid? DeploymentId) : IRequest<Result<AiChatResponse>>;

public class AiDeploymentChatCommandHandler : IRequestHandler<AiDeploymentChatCommand, Result<AiChatResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public AiDeploymentChatCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow; _currentUser = currentUser;
    }

    public async Task<Result<AiChatResponse>> Handle(AiDeploymentChatCommand request, CancellationToken ct)
    {
        // Simple rule-based response for common deployment questions
        var msg = request.Message.ToLowerInvariant();

        string reply;
        var quickReplies = new List<string>();

        if (msg.Contains("why") && (msg.Contains("fail") || msg.Contains("error")))
        {
            reply = "To diagnose the failure, I need to check your deployment logs. " +
                    "Use the **Analyze** button above to run a deep analysis and I'll identify the root cause and suggest fixes.";
            quickReplies = ["Run AI analysis", "Show error logs", "Check environment vars"];
        }
        else if (msg.Contains("rollback"))
        {
            reply = "To rollback: click the **Actions** menu on the deployment row and select **Rollback**. " +
                    "This will trigger a new deployment using the configuration from the previous successful deployment.";
            quickReplies = ["How to rollback", "What's canary deployment?"];
        }
        else if (msg.Contains("canary"))
        {
            reply = "**Canary releases** route a small percentage of traffic to the new version while the baseline remains live. " +
                    "Go to the deployment → **Canary** tab → set the traffic split (e.g. 10%) → click Start Canary. " +
                    "Monitor errors, then **Promote** to 100% or **Abort** to roll back automatically.";
            quickReplies = ["What traffic % should I start with?", "How to abort a canary?"];
        }
        else if (msg.Contains("performance") || msg.Contains("slow"))
        {
            reply = "For performance issues: 1) Check the **Server Metrics** for CPU/memory spikes. " +
                    "2) Review application traces for slow queries or external API calls. " +
                    "3) Consider enabling **horizontal scaling** in your Service configuration.";
            quickReplies = ["How to scale horizontally?", "Enable auto-scaling"];
        }
        else if (msg.Contains("cost") || msg.Contains("billing") || msg.Contains("expensive"))
        {
            reply = "Check the **Cost Analytics** page for a full breakdown by resource type with anomaly detection. " +
                    "Common savings: right-size idle servers, enable auto-scaling, remove unused volumes.";
            quickReplies = ["View cost analytics", "How to reduce compute costs?"];
        }
        else
        {
            reply = "I can help with deployment failures, performance issues, rollbacks, canary releases, and cost optimization. " +
                    "What specific issue are you investigating?";
            quickReplies = ["Analyze this deployment", "How to rollback?", "What is canary?", "Reduce costs"];
        }

        if (request.DeploymentId.HasValue)
        {
            var deployment = await _uow.Deployments.GetByIdAsync(request.DeploymentId.Value, ct);
            if (deployment?.TenantId != _currentUser.TenantId) { /* ignore, reply generically */ }
        }

        return Result<AiChatResponse>.Success(new AiChatResponse(reply, quickReplies));
    }
}
