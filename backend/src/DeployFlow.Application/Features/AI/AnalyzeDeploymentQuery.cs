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

        // ── Failure / error diagnosis ──────────────────────────────────────────
        if ((msg.Contains("why") && (msg.Contains("fail") || msg.Contains("error")))
            || msg.Contains("what went wrong") || msg.Contains("what's wrong")
            || msg.Contains("analyze recent") || msg.Contains("recent failure")
            || msg.Contains("diagnose") || msg.Contains("root cause")
            || (msg.Contains("analyze") && !msg.Contains("this deployment") && !request.DeploymentId.HasValue))
        {
            if (hasError)
            {
                var snippet = deploymentError!.Length > 250 ? deploymentError[..250] + "…" : deploymentError;
                reply = $"This deployment failed with:\n```\n{snippet}\n```\n\n{GetErrorGuidance(deploymentError)}";
                quickReplies = ["How do I fix this?", "Run full analysis", "How to rollback?"];
            }
            else
            {
                reply = "To diagnose the failure, click **Analyze Deployment** above — it will scan all build logs and give you a root-cause analysis with actionable fix suggestions.\n\n" +
                        "Common causes to check manually:\n- SSH key or server unreachable\n- Missing environment variables\n- Docker build error (check Logs tab)\n- Insufficient server memory";
                quickReplies = ["Run AI analysis", "Check environment vars", "SSH connection issues"];
            }
        }
        // ── Fix / resolve ──────────────────────────────────────────────────────
        else if (msg.Contains("fix") || msg.Contains("how to") || msg.Contains("resolve") || msg.Contains("solve"))
        {
            if (hasError)
            {
                reply = GetDetailedFix(deploymentError!);
                quickReplies = ["How to rollback?", "Check environment vars", "Run full analysis"];
            }
            else
            {
                reply = "I need more context to suggest a fix. Try:\n\n" +
                        "1. Open the **Logs** tab to see the actual error output\n" +
                        "2. Click **Analyze Deployment** on the AI Debug tab — it will detect the root cause automatically\n" +
                        "3. Or describe the error you're seeing and I'll guide you through the fix";
                quickReplies = ["Run AI analysis", "SSH connection issues", "Docker build failed"];
            }
        }
        // ── Rollback ───────────────────────────────────────────────────────────
        else if (msg.Contains("rollback") || msg.Contains("roll back") || msg.Contains("revert") || msg.Contains("undo deploy"))
        {
            reply = "**How to rollback:**\n\n" +
                    "1. Go to **Deployments** list\n" +
                    "2. Find the last deployment with status **Healthy** ✅\n" +
                    "3. Click into it → hit **Re-deploy** in the top-right corner\n\n" +
                    "This re-runs that exact commit and environment config without touching your repository. The rollback typically completes in the same time as your normal build.\n\n" +
                    "**Pro tip:** If you need zero-downtime rollback, use **Blue/Green** strategy — the old slot stays running until the new one is healthy.";
            quickReplies = ["What is blue/green?", "How to prevent future failures?", "What is canary?"];
        }
        // ── Canary ─────────────────────────────────────────────────────────────
        else if (msg.Contains("canary"))
        {
            reply = "**Canary releases** let you ship to a small % of traffic first, reducing blast radius:\n\n" +
                    "**Setup:**\n" +
                    "1. Open a healthy deployment → **Canary** tab\n" +
                    "2. Set traffic % (10% is a safe start)\n" +
                    "3. Click **Start Canary** — old version keeps serving 90% of traffic\n\n" +
                    "**Monitoring period:** Watch your error rate and latency for 10–30 min\n\n" +
                    "**Promote** → sends 100% traffic to new version\n" +
                    "**Abort** → instantly reverts all traffic to the old version\n\n" +
                    "**Recommended thresholds:** Promote if error rate < 0.5% and p99 latency is within 20% of baseline.";
            quickReplies = ["What is blue/green?", "How to rollback?", "Start at 10% or 20%?"];
        }
        // ── Blue/Green ────────────────────────────────────────────────────────
        else if (msg.Contains("blue") || msg.Contains("green") || msg.Contains("blue/green") || msg.Contains("zero downtime") || msg.Contains("zero-downtime"))
        {
            reply = "**Blue/Green deployment** runs two identical environments — Blue (current live) and Green (new version):\n\n" +
                    "1. **Deploy** to the Green slot while Blue keeps serving all traffic\n" +
                    "2. **Health check** verifies Green is stable\n" +
                    "3. **Traffic switch** — router instantly points to Green (< 1s cutover)\n" +
                    "4. Blue stays on standby for instant rollback if needed\n\n" +
                    "**To use:** Deploy dialog → Strategy → **Blue/Green**\n\n" +
                    "**Best for:** APIs with strict uptime SLAs, database migrations, large version changes.";
            quickReplies = ["What is canary?", "How to rollback?", "When to use blue/green vs canary?"];
        }
        // ── Performance / slow ────────────────────────────────────────────────
        else if (msg.Contains("performance") || msg.Contains("slow") || msg.Contains("latency") || msg.Contains("response time"))
        {
            reply = "**Performance troubleshooting checklist:**\n\n" +
                    "**1. Server resources**\n" +
                    "- Open **Infrastructure → Servers** → check CPU/memory graphs\n" +
                    "- Spikes above 80% CPU = needs scaling or optimization\n\n" +
                    "**2. Container resources**\n" +
                    "- Go to Service → Resources → increase CPU/memory limits\n" +
                    "- Enable **Auto-scaling** to handle traffic bursts\n\n" +
                    "**3. Database**\n" +
                    "- Add indexes on frequently queried columns\n" +
                    "- Enable connection pooling (PgBouncer for PostgreSQL)\n" +
                    "- For Oracle: check `V$SQL` for top slow queries\n\n" +
                    "**4. Application**\n" +
                    "- Enable response caching for static/semi-static data\n" +
                    "- Use async I/O throughout — never block on DB calls";
            quickReplies = ["How to enable auto-scaling?", "Database optimization tips", "How to scale horizontally?"];
        }
        // ── Scaling ────────────────────────────────────────────────────────────
        else if (msg.Contains("scal") || msg.Contains("replica") || msg.Contains("horizontal") || msg.Contains("load balanc"))
        {
            reply = "**Scaling your deployment:**\n\n" +
                    "**Horizontal scaling (more instances):**\n" +
                    "- Go to Service → Resources → set **Min/Max Replicas**\n" +
                    "- Set **CPU target %** (e.g. 70%) — DeployFlow auto-scales when exceeded\n" +
                    "- Ensure your app is **stateless** (no local file state, sessions in Redis)\n\n" +
                    "**Vertical scaling (bigger server):**\n" +
                    "- Increase CPU/memory limits in Service → Resources\n" +
                    "- Resize the underlying server if limits are maxed out\n\n" +
                    "**When to use each:**\n" +
                    "- Horizontal: variable traffic, stateless services, web APIs\n" +
                    "- Vertical: stateful workloads, databases, single-threaded apps";
            quickReplies = ["What is canary?", "Performance troubleshooting", "How to reduce costs?"];
        }
        // ── Docker ────────────────────────────────────────────────────────────
        else if (msg.Contains("docker") || msg.Contains("container") || msg.Contains("dockerfile") || msg.Contains("image"))
        {
            reply = "**Docker best practices for DeployFlow:**\n\n" +
                    "**Dockerfile optimization:**\n" +
                    "```dockerfile\n# Use specific version tags, not :latest\nFROM node:20-alpine\n\n# Copy package files first (layer cache)\nCOPY package*.json ./\nRUN npm ci --only=production\n\n# Then copy source\nCOPY . .\nRUN npm run build\n\n# Non-root user for security\nUSER node\n```\n\n" +
                    "**Key tips:**\n" +
                    "- Use **multi-stage builds** to keep images small\n" +
                    "- Add `.dockerignore` (exclude `node_modules`, `.git`, `.env`)\n" +
                    "- Set memory limits to prevent OOM kills\n" +
                    "- Use **health checks** so DeployFlow knows when your app is ready";
            quickReplies = ["What is blue/green?", "How to add health checks?", "Docker build failed — why?"];
        }
        // ── SSH / connection ──────────────────────────────────────────────────
        else if (msg.Contains("ssh") || msg.Contains("connection") || msg.Contains("server unreachable") || msg.Contains("timeout"))
        {
            reply = "**SSH / server connection troubleshooting:**\n\n" +
                    "**1. Verify the SSH key**\n" +
                    "- Go to **Infrastructure → Servers** → click your server → **Test Connection**\n" +
                    "- If it fails, re-add the SSH key under Infrastructure → SSH Keys\n\n" +
                    "**2. Check server firewall**\n" +
                    "```bash\nsudo ufw status          # check port 22 is open\nsudo ufw allow 22/tcp    # open if blocked\n```\n\n" +
                    "**3. Verify SSH user permissions**\n" +
                    "```bash\n# On the server, check the authorized_keys file:\ncat ~/.ssh/authorized_keys\n```\n\n" +
                    "**4. Test manually**\n" +
                    "```bash\nssh -i your_key.pem user@your-server-ip\n```";
            quickReplies = ["Environment variable issues", "Docker build failed", "How to rollback?"];
        }
        // ── Environment variables ─────────────────────────────────────────────
        else if (msg.Contains("env") || msg.Contains("environment") || msg.Contains("variable") || msg.Contains("secret") || msg.Contains("config"))
        {
            reply = "**Managing environment variables in DeployFlow:**\n\n" +
                    "**Add/edit env vars:**\n" +
                    "Projects → your project → ⚙️ **Env Variables** (or the back-arrow page)\n\n" +
                    "**Common required variables:**\n" +
                    "```\nDATABASE_URL=postgresql://user:pass@host:5432/db\nJWT_SECRET=<random-32-char-string>\nENCRYPTION_SECRET=<random-32-char-string>\nNODE_ENV=production\n```\n\n" +
                    "**Security tips:**\n" +
                    "- Never commit `.env` to git — use DeployFlow's encrypted secret storage\n" +
                    "- Rotate secrets by updating the value and re-deploying\n" +
                    "- Use separate values for dev/staging/production\n\n" +
                    "**After changing env vars**, trigger a fresh deployment — running containers don't pick up changes automatically.";
            quickReplies = ["How to rollback?", "SSH connection issues", "Docker best practices"];
        }
        // ── Optimize deployment ───────────────────────────────────────────────
        else if (msg.Contains("optim") || msg.Contains("speed up") || msg.Contains("faster") || msg.Contains("build time"))
        {
            reply = "**Deployment optimization strategies:**\n\n" +
                    "**Faster builds:**\n" +
                    "- Order Dockerfile layers so dependencies are cached (COPY package.json before COPY .)\n" +
                    "- Use `npm ci` instead of `npm install` in CI\n" +
                    "- Enable **BuildKit** (`DOCKER_BUILDKIT=1`) for parallel layer building\n\n" +
                    "**Faster deploys:**\n" +
                    "- Use **Blue/Green** to deploy in parallel with the live instance\n" +
                    "- Pre-pull base images on the server: `docker pull node:20-alpine`\n" +
                    "- Keep images small — multi-stage builds, Alpine base images\n\n" +
                    "**Reduce downtime:**\n" +
                    "- Add a health check endpoint `/health` that returns 200 when ready\n" +
                    "- Set `startPeriod` in your health check to give the app time to start\n" +
                    "- Use rolling deploys or blue/green for zero-downtime cutover";
            quickReplies = ["What is blue/green?", "Docker best practices", "How to add health checks?"];
        }
        // ── Health checks ─────────────────────────────────────────────────────
        else if (msg.Contains("health") || msg.Contains("health check") || msg.Contains("healthcheck"))
        {
            reply = "**Health checks ensure DeployFlow only routes traffic to a ready container:**\n\n" +
                    "**Add a health endpoint to your app:**\n" +
                    "```js\n// Express / Node.js\napp.get('/health', (req, res) => res.json({ status: 'ok' }));\n```\n" +
                    "```csharp\n// ASP.NET Core\napp.MapHealthChecks(\"/health\");\n```\n\n" +
                    "**In your Dockerfile:**\n" +
                    "```dockerfile\nHEALTHCHECK --interval=10s --timeout=5s --retries=3 \\\n  --start-period=30s \\\n  CMD curl -f http://localhost:3000/health || exit 1\n```\n\n" +
                    "The `start-period` gives your app time to boot before checks begin — increase it for slow-starting apps.";
            quickReplies = ["Docker best practices", "How to optimize deployment?", "What is canary?"];
        }
        // ── Cost / billing ────────────────────────────────────────────────────
        else if (msg.Contains("cost") || msg.Contains("billing") || msg.Contains("expensive") || msg.Contains("saving") || msg.Contains("cheap"))
        {
            reply = "**Cost optimization in DeployFlow:**\n\n" +
                    "**Quick wins:**\n" +
                    "- Open **Cost Analytics** → view spend by project/server\n" +
                    "- Right-size servers: if CPU stays under 20%, downsize\n" +
                    "- Delete stopped containers and unused volumes\n" +
                    "- Use auto-scaling to scale down during off-peak hours\n\n" +
                    "**Medium-term:**\n" +
                    "- Move dev/staging to smaller servers (or stop them overnight)\n" +
                    "- Use spot/preemptible instances for non-critical workloads\n" +
                    "- Share databases between staging projects where compatible\n\n" +
                    "**Monitoring:** Set up **cost alerts** in Alerts → New Rule → Metric: `MonthlyCost` to get notified before bills spike.";
            quickReplies = ["How to enable auto-scaling?", "Right-sizing servers", "Set up cost alerts"];
        }
        // ── Logs ──────────────────────────────────────────────────────────────
        else if (msg.Contains("log") || msg.Contains("trace") || msg.Contains("output") || msg.Contains("stdout") || msg.Contains("stderr"))
        {
            reply = "**Finding logs in DeployFlow:**\n\n" +
                    "**Build logs:** Deployment detail page → **Logs** tab → live-streamed during deploy\n\n" +
                    "**Runtime logs:** Infrastructure → Servers → click server → **Containers** → your container → **Logs**\n\n" +
                    "**Structured logs:** Use the **Logs** sidebar (left nav) for cross-project log search by level, service, or keyword\n\n" +
                    "**Tips:**\n" +
                    "- Filter by `stderr` stream to see only errors\n" +
                    "- Build logs are stored in the DB — accessible even after the deployment finishes\n" +
                    "- For long-running builds, the live log streams in real-time via SignalR" +
                    (hasError ? $"\n\n**Current error in this deployment:**\n```\n{deploymentError}\n```" : "");
            quickReplies = ["Why did this fail?", "Run full analysis", "SSH connection issues"];
        }
        // ── Pipeline / CI ─────────────────────────────────────────────────────
        else if (msg.Contains("pipeline") || msg.Contains("ci") || msg.Contains("cd") || msg.Contains("ci/cd") || msg.Contains("automat") || msg.Contains("trigger"))
        {
            reply = "**Setting up CI/CD pipelines in DeployFlow:**\n\n" +
                    "**Auto-deploy on git push:**\n" +
                    "- Project Settings → **Auto Deploy** → enable → set branch (e.g. `main`)\n" +
                    "- Each push to that branch triggers a deployment automatically\n\n" +
                    "**Custom pipelines:**\n" +
                    "- **Pipelines** (left nav) → New Pipeline\n" +
                    "- Add stages: Test → Build → Deploy → Smoke Test\n" +
                    "- Each stage has steps (shell commands, Docker commands, etc.)\n\n" +
                    "**Webhook triggers:**\n" +
                    "- Settings → Webhooks → copy the URL into GitHub/GitLab\n" +
                    "- Trigger type: `push` or `pull_request`\n\n" +
                    "**Best practice:** Gate production deploys behind a manual approval step.";
            quickReplies = ["What is canary?", "What is blue/green?", "How to set up auto-scaling?"];
        }
        // ── Database ──────────────────────────────────────────────────────────
        else if (msg.Contains("database") || msg.Contains("postgres") || msg.Contains("oracle") || msg.Contains("mongo") || msg.Contains("migration") || msg.Contains("sql"))
        {
            reply = "**Database management in DeployFlow:**\n\n" +
                    "**Provision a database:**\n" +
                    "- **Databases** (left nav) → New Database → choose PostgreSQL / MySQL / MongoDB\n" +
                    "- DeployFlow provisions it in Docker, exposes the connection string as an env var\n\n" +
                    "**Backups:**\n" +
                    "- Database → Settings → **Backup Policy**\n" +
                    "- Set schedule (e.g. daily at 02:00), retention (7–30 days), S3 destination\n\n" +
                    "**Migrations:**\n" +
                    "- Run migrations as a **pipeline stage** before the deploy step\n" +
                    "- For Entity Framework: `dotnet ef database update` in a pre-deploy hook\n\n" +
                    "**Connection string format:**\n" +
                    "```\npostgresql://user:password@host:5432/dbname\noracle://user:password@host:1521/servicename\n```";
            quickReplies = ["How to set up backups?", "Database performance tips", "Environment variables"];
        }
        // ── What is / explain ─────────────────────────────────────────────────
        else if (msg.StartsWith("what is") || msg.StartsWith("explain") || msg.StartsWith("tell me about") || msg.StartsWith("how does"))
        {
            if (msg.Contains("blue") || msg.Contains("green"))
            {
                reply = "**Blue/Green deployment** is a zero-downtime release strategy:\n\n" +
                        "- **Blue** = current live environment\n- **Green** = new version being deployed\n\n" +
                        "While Green is being built and tested, Blue handles all traffic. Once Green passes health checks, the load balancer switches instantly. Blue stays on standby for instant rollback.";
                quickReplies = ["What is canary?", "How to rollback?", "Set up blue/green"];
            }
            else if (msg.Contains("canary"))
            {
                reply = "**Canary release** gradually shifts traffic to the new version:\n\n" +
                        "Start at 5–10%, watch metrics, then promote to 100% if healthy — or abort instantly if errors appear. Named after the \"canary in a coal mine\" — small exposure, early warning.";
                quickReplies = ["What is blue/green?", "How to start a canary?", "What traffic % is safe?"];
            }
            else if (msg.Contains("deploy") || msg.Contains("deployflow"))
            {
                reply = "**DeployFlow** is your self-hosted deployment platform. It:\n\n" +
                        "- Pulls code from your Git repo on each deploy trigger\n" +
                        "- Auto-detects your stack (Next.js, .NET, Node, Python, etc.)\n" +
                        "- Generates and runs a Docker build on your remote server via SSH\n" +
                        "- Streams build logs in real-time to the UI\n" +
                        "- Manages canary, blue/green, rollbacks, and health checks\n" +
                        "- Monitors server metrics, costs, and alerts";
                quickReplies = ["How to set up auto-deploy?", "What is canary?", "How to add a server?"];
            }
            else
            {
                reply = "I can explain any DevOps concept — try asking specifically about:\n" +
                        "- \"What is blue/green deployment?\"\n- \"What is canary release?\"\n- \"How does Docker health check work?\"\n- \"What is auto-scaling?\"\n- \"How does CI/CD work?\"";
                quickReplies = ["What is blue/green?", "What is canary?", "How does CI/CD work?"];
            }
        }
        // ── Performance / slow ────────────────────────────────────────────────
        else if (msg.Contains("best practice") || msg.Contains("recommend") || msg.Contains("tip"))
        {
            reply = "**DeployFlow best practices:**\n\n" +
                    "🔒 **Security**\n" +
                    "- Rotate JWT_SECRET and ENCRYPTION_SECRET every 90 days\n" +
                    "- Use SSH keys, not passwords, for server access\n" +
                    "- Run containers as non-root users\n\n" +
                    "🚀 **Reliability**\n" +
                    "- Always add a `/health` endpoint and configure health checks\n" +
                    "- Use Blue/Green or Canary for production deployments\n" +
                    "- Keep rollback deployments available (don't delete old healthy deploys)\n\n" +
                    "⚡ **Performance**\n" +
                    "- Multi-stage Dockerfiles — final image should be < 200MB\n" +
                    "- Enable auto-scaling with a CPU target of 70%\n" +
                    "- Use connection pooling for databases\n\n" +
                    "💰 **Cost**\n" +
                    "- Stop dev servers overnight with scheduled tasks\n" +
                    "- Set cost alerts to catch unexpected spend early";
            quickReplies = ["Docker best practices", "How to set up canary?", "How to scale my app?"];
        }
        else
        {
            // Context-aware default
            if (hasError)
            {
                var snippet = deploymentError!.Length > 150 ? deploymentError[..150] + "…" : deploymentError;
                reply = $"This deployment is in **{deploymentStatus ?? "unknown"}** state with error:\n```\n{snippet}\n```\n\n" +
                        "Ask me: \"Why did this fail?\" or click **Analyze Deployment** for a full root-cause analysis.";
                quickReplies = ["Why did this fail?", "How to fix?", "Run full analysis"];
            }
            else if (deploymentStatus != null)
            {
                reply = $"This deployment is currently **{deploymentStatus}**" +
                        (deploymentBranch != null ? $" on branch `{deploymentBranch}`" : "") + ".\n\n" +
                        "I can help with:\n- Failures & root cause analysis\n- Rollbacks & canary releases\n- Performance & scaling\n- Docker & environment config\n\nWhat would you like to know?";
                quickReplies = ["Analyze this deployment", "How to rollback?", "Performance tips"];
            }
            else
            {
                reply = "I'm your DeployFlow AI assistant. I can help with:\n\n" +
                        "- **Deployment failures** — root cause analysis and fixes\n" +
                        "- **Infrastructure** — server sizing, Docker, scaling\n" +
                        "- **CI/CD best practices** — pipelines, canary, blue/green\n" +
                        "- **Security** — secrets, SSH keys, container hardening\n" +
                        "- **Cost optimization** — right-sizing, idle resource cleanup\n\n" +
                        "Open a specific deployment and use the **AI Debug** tab for context-aware analysis, or ask me anything above!";
                quickReplies = ["How do I optimize my deployment?", "What is blue/green?", "Best practices for Docker"];
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
