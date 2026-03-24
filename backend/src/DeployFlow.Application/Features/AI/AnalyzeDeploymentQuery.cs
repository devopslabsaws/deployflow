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

        // collect up to 500 log lines + always include the stored ErrorMessage
        var (logs, _) = await _uow.Deployments.GetLogsPagedAsync(request.DeploymentId, 1, 500, ct);
        var logText   = string.Join("\n", logs.Select(l => $"[{l.Level}] {l.Message}"));
        var errors    = logs.Where(l => l.Level == LogLevel.Error).ToList();
        // Include deployment.ErrorMessage so pattern matching works even when logs are empty
        var errorText = string.Join("\n", new[] { deployment.ErrorMessage ?? "" }
            .Concat(errors.Select(l => l.Message))
            .Where(s => !string.IsNullOrEmpty(s)));

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
        // Cryptographic / AES key mismatch (e.g. ENCRYPTION_SECRET changed after data was stored)
        else if (Regex.IsMatch(errorText + logText, @"padding is invalid|CryptographicException|bad decrypt|Mac check.*failed|invalid.*padding|cannot be removed", RegexOptions.IgnoreCase))
        {
            severity  = "critical";
            diagnosis = "Cryptographic padding error: an AES decrypt operation failed. The stored data was encrypted with a different key than the one currently configured.";
            rootCause = "ENCRYPTION_SECRET was changed after encrypted data (refresh tokens, secrets) was written to the database. Decryption fails because the key no longer matches.";
            suggestions.AddRange([
                new("Restore original ENCRYPTION_SECRET", "Revert ENCRYPTION_SECRET in your .env to the value used when data was first encrypted. Check your deployment history or .env backup.", "config"),
                new("Clear all refresh tokens", "If the original key is unavailable, delete all rows from the refresh_tokens table to force users to log in again. This clears the encrypted data so the new key works.", "maintenance"),
                new("Implement key rotation properly", "Use a versioned encryption scheme: decrypt existing data with the old key, re-encrypt with the new key atomically, then retire the old key.", "security"),
            ]);
            autoFixes.Add(new("clear-sessions", "Clear all refresh tokens (force re-login)", "clear-refresh-tokens", true));
        }
        // JWT / authentication token errors
        else if (Regex.IsMatch(errorText + logText, @"SecurityTokenException|invalid.*token|token.*invalid|signature.*invalid|IDX\d{5}|401.*unauthorized", RegexOptions.IgnoreCase))
        {
            severity  = "high";
            diagnosis = "Authentication token validation failed. JWT tokens cannot be verified with the current signing key.";
            rootCause = "JWT_SECRET was changed, causing previously issued tokens to fail signature verification. All existing sessions are invalid.";
            suggestions.AddRange([
                new("Verify JWT_SECRET consistency", "Ensure JWT_SECRET in .env matches the value used when tokens were issued. A mismatch invalidates all active sessions.", "config"),
                new("Force re-login for all users", "If you intentionally rotated JWT_SECRET, clear stored refresh tokens so users get new tokens with the current secret.", "maintenance"),
            ]);
            autoFixes.Add(new("clear-sessions", "Clear refresh tokens (force re-login)", "clear-refresh-tokens", false));
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
            // fallback — show actual error text so the user sees something useful
            var primaryError = !string.IsNullOrEmpty(deployment.ErrorMessage)
                ? deployment.ErrorMessage
                : errors.FirstOrDefault()?.Message;

            severity  = !string.IsNullOrEmpty(primaryError) ? "medium" : "low";
            diagnosis = !string.IsNullOrEmpty(primaryError)
                ? $"Deployment failed with: \"{primaryError}\""
                : "Deployment did not complete successfully. No specific error pattern was identified.";
            rootCause = !string.IsNullOrEmpty(primaryError)
                ? $"Primary error: {primaryError}. Check the Logs tab for the full stack trace."
                : "No error pattern matched. Enable verbose logging or review the full log for more details.";
            suggestions.AddRange([
                new("Review full logs", "Open the Logs tab and look for the first ERROR or FATAL line — it usually points to the root cause.", "diagnostics"),
                new("Add verbose logging", "Enable debug-level logging (e.g., set Logging__LogLevel__Default=Debug) to expose more detail about the failure point.", "diagnostics"),
            ]);
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
        // ── Load deployment context FIRST so responses include real error details ─
        string? deploymentError = null;
        string? deploymentStatus = null;
        string? deploymentBranch = null;

        if (request.DeploymentId.HasValue)
        {
            var dep = await _uow.Deployments.GetByIdAsync(request.DeploymentId.Value, ct);
            if (dep?.TenantId == _currentUser.TenantId)
            {
                deploymentStatus = dep.Status.ToString();
                deploymentBranch = dep.Branch;
                deploymentError  = dep.ErrorMessage;

                // If no stored error message, pull from error logs
                if (string.IsNullOrEmpty(deploymentError))
                {
                    var (logItems, _) = await _uow.Deployments.GetLogsPagedAsync(request.DeploymentId.Value, 1, 30, ct);
                    deploymentError = logItems.FirstOrDefault(l => l.Level == LogLevel.Error)?.Message;
                }
            }
        }

        var msg = request.Message.ToLowerInvariant();
        bool hasError = !string.IsNullOrEmpty(deploymentError);
        string reply;
        var quickReplies = new List<string>();

        // ── Failure / error questions — always answer with real error context ──
        if (msg.Contains("why") && (msg.Contains("fail") || msg.Contains("error"))
            || msg.Contains("what went wrong") || msg.Contains("what's wrong")
            || msg.Contains("issue") || msg.Contains("problem"))
        {
            if (hasError)
            {
                var snippet = deploymentError!.Length > 250 ? deploymentError[..250] + "…" : deploymentError;
                reply = $"This deployment failed with:\n```\n{snippet}\n```\n\n{GetErrorGuidance(deploymentError)}";
                quickReplies = ["How do I fix this?", "Run full analysis", "How to rollback?"];
            }
            else
            {
                reply = "To diagnose the failure, click **Analyze Deployment** above. It will scan deployment logs and give you a root-cause analysis with actionable fix suggestions.";
                quickReplies = ["Run AI analysis", "Check environment vars"];
            }
        }
        // ── How to fix this specific error ────────────────────────────────────
        else if (msg.Contains("fix") || msg.Contains("how to") || msg.Contains("resolve") || msg.Contains("solve"))
        {
            if (hasError)
            {
                reply = GetDetailedFix(deploymentError!);
                quickReplies = ["How to rollback?", "Check environment vars", "Run full analysis"];
            }
            else
            {
                reply = "Please click **Analyze Deployment** first — I'll then generate targeted fix steps based on the actual error.";
                quickReplies = ["Run AI analysis"];
            }
        }
        else if (msg.Contains("rollback"))
        {
            reply = "To rollback: go to **Deployments**, find the last successful deploy, click its **⋮ menu** → **Re-deploy**. " +
                    "This re-runs that exact commit and configuration without needing to revert your repository.";
            quickReplies = ["What is canary?", "How to prevent future failures?"];
        }
        else if (msg.Contains("canary"))
        {
            reply = "**Canary releases** send a small traffic slice (e.g. 10%) to the new version while keeping the old version live. " +
                    "Open this deployment → **Canary** tab → set a traffic % → click **Start Canary**. " +
                    "If metrics stay healthy, click **Promote** to go to 100%; otherwise click **Abort** to roll back instantly.";
            quickReplies = ["Start at 10% or 20%?", "How to abort a canary?"];
        }
        else if (msg.Contains("performance") || msg.Contains("slow"))
        {
            reply = "For performance issues:\n1. Check **Server Metrics** (CPU/memory graphs) for spikes at the time of slowness.\n" +
                    "2. Look for slow DB queries in your application logs.\n" +
                    "3. Enable **Auto-scaling** in Service → Resources to handle traffic surges automatically.";
            quickReplies = ["Enable auto-scaling", "How to scale horizontally?"];
        }
        else if (msg.Contains("cost") || msg.Contains("billing") || msg.Contains("expensive"))
        {
            reply = "Open **Cost Analytics** for a breakdown by resource. Top savings levers: right-size idle servers, enable auto-scaling to avoid over-provisioning, and remove unused volumes.";
            quickReplies = ["How to reduce compute costs?"];
        }
        else if (msg.Contains("log") || msg.Contains("trace"))
        {
            reply = $"Open the **Logs** tab on this deployment to see the full build and runtime output." +
                    (deploymentStatus != null ? $" Current status: **{deploymentStatus}**{(deploymentBranch != null ? $" (branch: {deploymentBranch})" : "")}." : "") +
                    (hasError ? $"\n\nLast recorded error: `{deploymentError}`" : "");
            quickReplies = ["Why did this fail?", "Run full analysis"];
        }
        else
        {
            // Context-aware default: surface the error if we have it
            if (hasError)
            {
                var snippet = deploymentError!.Length > 150 ? deploymentError[..150] + "…" : deploymentError;
                reply = $"This deployment is in **{deploymentStatus ?? "unknown"}** state with error: `{snippet}`\n\n" +
                        "I can help diagnose this. Ask me: \"Why did this fail?\" or click **Analyze Deployment** for a full AI analysis.";
                quickReplies = ["Why did this fail?", "How to fix?", "Run full analysis"];
            }
            else
            {
                reply = $"This deployment is in **{deploymentStatus ?? "unknown"}** state" +
                        (deploymentBranch != null ? $" on branch **{deploymentBranch}**" : "") + ".\n\n" +
                        "I can help with failures, rollbacks, canary releases, and performance issues. What would you like to know?";
                quickReplies = ["Analyze this deployment", "How to rollback?", "What is canary?"];
            }
        }

        return Result<AiChatResponse>.Success(new AiChatResponse(reply, quickReplies));
    }

    private static string GetErrorGuidance(string error)
    {
        var e = error.ToLowerInvariant();
        if (e.Contains("padding is invalid") || e.Contains("cryptographicexception") || e.Contains("bad decrypt"))
            return "**Root cause:** ENCRYPTION_SECRET was changed after encrypted data was stored. " +
                   "**Fix:** Restore the original ENCRYPTION_SECRET in your `.env`, OR delete all refresh tokens (forces re-login) if the original key is unavailable.";
        if (e.Contains("securitytokenexception") || e.Contains("invalid token") || (e.Contains("jwt") && e.Contains("invalid")))
            return "**Root cause:** JWT_SECRET mismatch — existing tokens are signed with a different key. " +
                   "**Fix:** Restore the original JWT_SECRET, or clear refresh tokens to force users to re-authenticate with the new key.";
        if (e.Contains("connection refused") || e.Contains("econnrefused"))
            return "**Root cause:** A downstream service (DB, Redis, API) is unreachable. " +
                   "**Fix:** Check connection strings in environment variables, verify the target service is running, and confirm firewall rules allow the connection.";
        if (e.Contains("out of memory") || e.Contains("oom") || e.Contains("exit code 137"))
            return "**Root cause:** Container killed by OOM. **Fix:** Increase the memory limit in Service → Resources, or add memory profiling to find the leak.";
        if (e.Contains("npm") && (e.Contains("error") || e.Contains("failed")))
            return "**Root cause:** npm install failed. **Fix:** Check package.json for invalid versions, clear npm cache (`npm cache clean --force`), and ensure a compatible Node.js version.";
        if (e.Contains("dockerfile") || (e.Contains("no such file") && e.Contains("copy")))
            return "**Root cause:** Docker build context error. **Fix:** Verify all COPY/ADD paths in your Dockerfile exist in the repository root.";
        return "Click **Analyze Deployment** on the AI Debug tab for a full pattern-matched diagnosis, or review the **Logs** tab for the complete error trace.";
    }

    private static string GetDetailedFix(string error)
    {
        var e = error.ToLowerInvariant();
        if (e.Contains("padding is invalid") || e.Contains("cryptographicexception"))
            return "**Fix for Encryption Key Mismatch:**\n" +
                   "1. Go to your server `.env` (or Secrets configuration) and revert `ENCRYPTION_SECRET` to the previous value.\n" +
                   "2. If the original key is lost, connect to Oracle and run: `DELETE FROM \"RefreshTokens\";` then `COMMIT;` — this forces all users to log in again with the new key.\n" +
                   "3. Restart the API container after the change.";
        if (e.Contains("connection refused") || e.Contains("econnrefused"))
            return "**Fix for Connection Refused:**\n" +
                   "1. Verify the `ConnectionStrings__DefaultConnection` env var points to the correct host/port.\n" +
                   "2. On the server, run `docker compose ps` to confirm all service containers are running.\n" +
                   "3. Check firewall rules: `sudo ufw status` — ensure the DB port is open.";
        if (e.Contains("out of memory") || e.Contains("exit code 137"))
            return "**Fix for OOM Kill:**\n" +
                   "1. Go to Service → Resources and increase the memory limit (try doubling it).\n" +
                   "2. Add `--max-old-space-size=512` (Node.js) or similar limits to your start command.\n" +
                   "3. Profile heap usage with `dotnet-dump` (.NET) or `node --heap-prof` (Node.js) to find the leak.";
        return "Click **Analyze Deployment** for a full diagnosis with automated fix suggestions tailored to this error.";
    }
}
