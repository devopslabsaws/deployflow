using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Insights;

// ─── Deployment Insights ──────────────────────────────────────────────────────

public record DailyDeployStat(string Date, int Total, int Succeeded, int Failed, double AvgDurationSeconds);

public record DeploymentInsightsDto(
    int TotalDeploys,
    int SuccessCount,
    int FailedCount,
    double SuccessRate,
    double AvgDurationSeconds,
    int FastestDeploy,
    int SlowestDeploy,
    List<DailyDeployStat> DailyStats,
    Dictionary<string, int> FailuresByProject,
    Dictionary<string, int> DeploysByTrigger,
    string PeriodLabel
);

public record GetDeploymentInsightsQuery(string Period = "30d") : IRequest<Result<DeploymentInsightsDto>>;

public class GetDeploymentInsightsQueryHandler
    : IRequestHandler<GetDeploymentInsightsQuery, Result<DeploymentInsightsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetDeploymentInsightsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<DeploymentInsightsDto>> Handle(
        GetDeploymentInsightsQuery request, CancellationToken ct)
    {
        var since = ParsePeriod(request.Period);
        var tid = _currentUser.TenantId;

        var deployments = await _uow.Deployments.FindAsync(
            d => d.TenantId == tid && d.CreatedAt >= since, ct);

        if (!deployments.Any())
            return Result<DeploymentInsightsDto>.Success(Empty(request.Period));

        var items = deployments.Select(d => new
        {
            d.Status,
            d.DurationSeconds,
            d.ProjectId,
            d.Trigger,
            Date = d.CreatedAt.Date,
            d.CreatedAt
        }).ToList();

        var total = items.Count;
        var succeeded = items.Count(d => d.Status == DeploymentStatus.Healthy);
        var failed = items.Count(d => d.Status == DeploymentStatus.Failed);
        var durations = items
            .Where(d => d.DurationSeconds.HasValue)
            .Select(d => d.DurationSeconds!.Value).ToList();

        var avgDur = durations.Count > 0 ? durations.Average() : 0;
        var fastest = durations.Count > 0 ? durations.Min() : 0;
        var slowest = durations.Count > 0 ? durations.Max() : 0;

        // Daily breakdown
        var dailyStats = items
            .GroupBy(d => d.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var daySucc = g.Count(d => d.Status == DeploymentStatus.Healthy);
                var dayFail = g.Count(d => d.Status == DeploymentStatus.Failed);
                var dayDurs = g.Where(d => d.DurationSeconds.HasValue)
                               .Select(d => (double)d.DurationSeconds!.Value).ToList();
                return new DailyDeployStat(
                    g.Key.ToString("yyyy-MM-dd"),
                    g.Count(),
                    daySucc,
                    dayFail,
                    dayDurs.Count > 0 ? Math.Round(dayDurs.Average()) : 0);
            })
            .ToList();

        // Failures by project
        var failuresByProject = items
            .Where(d => d.Status == DeploymentStatus.Failed)
            .GroupBy(d => d.ProjectId.ToString()[..8])
            .ToDictionary(g => g.Key, g => g.Count());

        // Deploys by trigger
        var byTrigger = items
            .GroupBy(d => d.Trigger.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        return Result<DeploymentInsightsDto>.Success(new DeploymentInsightsDto(
            total, succeeded, failed,
            total > 0 ? Math.Round((double)succeeded / total * 100, 1) : 0,
            Math.Round(avgDur, 1),
            fastest, slowest,
            dailyStats,
            failuresByProject,
            byTrigger,
            PeriodLabel(request.Period)));
    }

    private static DateTime ParsePeriod(string period) => period switch
    {
        "7d" => DateTime.UtcNow.AddDays(-7),
        "14d" => DateTime.UtcNow.AddDays(-14),
        "30d" => DateTime.UtcNow.AddDays(-30),
        "90d" => DateTime.UtcNow.AddDays(-90),
        _ => DateTime.UtcNow.AddDays(-30)
    };

    private static string PeriodLabel(string period) => period switch
    {
        "7d" => "Last 7 days",
        "14d" => "Last 14 days",
        "30d" => "Last 30 days",
        "90d" => "Last 90 days",
        _ => "Last 30 days"
    };

    private static DeploymentInsightsDto Empty(string period) => new(
        0, 0, 0, 0, 0, 0, 0, [], [], [], PeriodLabel(period));
}

// ─── Smart Error Intelligence ─────────────────────────────────────────────────

public record ErrorSuggestion(
    string Category,       // "port-conflict" | "missing-env" | "oom" | "build-failure" | "db-connection"
    string Title,
    string Description,
    string Fix,
    string Severity       // "error" | "warning" | "info"
);

public record AnalyzeDeploymentErrorsQuery(Guid DeploymentId) : IRequest<Result<List<ErrorSuggestion>>>;

public class AnalyzeDeploymentErrorsQueryHandler
    : IRequestHandler<AnalyzeDeploymentErrorsQuery, Result<List<ErrorSuggestion>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public AnalyzeDeploymentErrorsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<ErrorSuggestion>>> Handle(
        AnalyzeDeploymentErrorsQuery request, CancellationToken ct)
    {
        var (logItems, _) = await _uow.Deployments.GetLogsPagedAsync(
            request.DeploymentId, page: 1, pageSize: 500, ct);
        var logs = logItems.Select(l => l.Message).ToList();

        if (!logs.Any())
            return Result<List<ErrorSuggestion>>.Success([]);

        var fullLog = string.Join("\n", logs);
        var suggestions = Analyze(fullLog);
        return Result<List<ErrorSuggestion>>.Success(suggestions);
    }

    internal static List<ErrorSuggestion> Analyze(string log)
    {
        var results = new List<ErrorSuggestion>();
        var l = log.ToLowerInvariant();

        // Port conflicts
        if (l.Contains("eaddrinuse") || l.Contains("address already in use") || l.Contains("port is already allocated"))
            results.Add(new("port-conflict", "Port Already In Use",
                "Another process is already bound to the target port.",
                "Set a different PORT environment variable, or stop the conflicting service on the server.",
                "error"));

        // Missing env vars
        var missingEnvPatterns = new[] { "is not defined", "undefined environment variable", "env.*not.*set", "missing.*required.*env" };
        if (missingEnvPatterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(l, p)))
            results.Add(new("missing-env", "Missing Environment Variable",
                "A required environment variable is not configured.",
                "Go to Project → Environment Variables and add the missing variable. Check the deployment log for the variable name.",
                "error"));

        // Out of memory
        if (l.Contains("out of memory") || l.Contains("enomem") || l.Contains("oom") ||
            l.Contains("memory limit exceeded") || l.Contains("killed") && l.Contains("memory"))
            results.Add(new("oom", "Out of Memory (OOM Kill)",
                "The container was killed because it exceeded its memory limit.",
                "Increase the memory limit in Service → Resources, or optimize your application's memory usage.",
                "error"));

        // DB connection issues
        var dbPatterns = new[] { "connection refused", "cannot connect to database", "pg_connect failed",
            "access denied for user", "could not connect to server", "etimedout.*database", "sqlexception" };
        if (dbPatterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(l, p)))
            results.Add(new("db-connection", "Database Connection Failed",
                "The application cannot establish a connection to the database.",
                "Verify DATABASE_URL / connection string is correct, the database server is running, and network/firewall rules allow the connection.",
                "error"));

        // npm/pip/go install failures
        if (l.Contains("npm err") || l.Contains("npm error") ||
            l.Contains("errno") && l.Contains("npm"))
            results.Add(new("build-failure", "npm Install Failed",
                "Failed to install Node.js packages.",
                "Check package.json for invalid versions. Try clearing cache: `npm cache clean --force`. Delete node_modules and package-lock.json, then retry.",
                "error"));

        if (l.Contains("pip install") && (l.Contains("error") || l.Contains("failed")))
            results.Add(new("build-failure", "pip Install Failed",
                "Failed to install Python packages.",
                "Check requirements.txt for version conflicts. Try upgrading pip: `pip install --upgrade pip`.",
                "error"));

        // Missing Dockerfile
        if (l.Contains("no such file or directory") && l.Contains("dockerfile"))
            results.Add(new("build-failure", "Dockerfile Not Found",
                "No Dockerfile was found in the project root.",
                "Use the Stack Detection feature to auto-generate a Dockerfile, or create one manually at the repository root.",
                "error"));

        // Permission issues
        if (l.Contains("permission denied") || l.Contains("eacces") || l.Contains("enoent") && l.Contains("permission"))
            results.Add(new("permission", "Permission Denied",
                "A file operation was blocked due to insufficient permissions.",
                "Ensure your Dockerfile runs as a non-root user and that file ownership is correct.",
                "warning"));

        // SSL / TLS
        if (l.Contains("certificate") && (l.Contains("expired") || l.Contains("invalid") || l.Contains("ssl")))
            results.Add(new("ssl", "SSL/TLS Certificate Issue",
                "There is a problem with the SSL certificate.",
                "Check your domain configuration. If using Let's Encrypt, verify DNS records point to your server and port 80 is accessible.",
                "warning"));

        // Build timeout
        if (l.Contains("timeout") || l.Contains("timed out"))
            results.Add(new("timeout", "Build/Deploy Timeout",
                "The operation exceeded its time limit.",
                "Optimize your build (enable build caching, reduce layers). Increase server resources if the build is compute-intensive.",
                "warning"));

        // Docker daemon issues
        if (l.Contains("docker daemon") || l.Contains("cannot connect to the docker") ||
            l.Contains("is the docker daemon running"))
            results.Add(new("docker-daemon", "Docker Daemon Not Running",
                "Cannot connect to the Docker daemon on the target server.",
                "SSH into the server and run: `sudo systemctl restart docker`",
                "error"));

        // Healthy / no errors
        if (results.Count == 0 && (l.Contains("health check pass") || l.Contains("deploy successful") ||
            l.Contains("deployment complete")))
            results.Add(new("info", "No Issues Detected",
                "The deployment appears to have completed successfully.",
                "If the app isn't responding, check health check endpoint configuration.",
                "info"));

        return results;
    }
}

// ─── Outbound Webhooks ────────────────────────────────────────────────────────

public record OutboundWebhookDto(
    Guid Id, string Name, string Url, string Events, bool IsEnabled, Guid? ProjectId,
    int DeliveryCount, int FailureCount, DateTime? LastDeliveredAt, string? LastResponseStatus);

public record GetOutboundWebhooksQuery(Guid? ProjectId) : IRequest<Result<List<OutboundWebhookDto>>>;

public class GetOutboundWebhooksQueryHandler
    : IRequestHandler<GetOutboundWebhooksQuery, Result<List<OutboundWebhookDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetOutboundWebhooksQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<OutboundWebhookDto>>> Handle(
        GetOutboundWebhooksQuery request, CancellationToken ct)
    {
        var hooks = await _uow.OutboundWebhooks.GetByTenantAsync(_currentUser.TenantId, ct);
        var filtered = request.ProjectId.HasValue
            ? hooks.Where(h => h.ProjectId == null || h.ProjectId == request.ProjectId.Value)
            : hooks;

        return Result<List<OutboundWebhookDto>>.Success(filtered.Select(Map).ToList());
    }

    internal static OutboundWebhookDto Map(OutboundWebhookConfig h) => new(
        h.Id, h.Name, h.Url, h.Events, h.IsEnabled, h.ProjectId,
        h.DeliveryCount, h.FailureCount, h.LastDeliveredAt, h.LastResponseStatus);
}

public record CreateOutboundWebhookCommand(
    string Name, string Url, string Events, string? Secret, Guid? ProjectId
) : IRequest<Result<OutboundWebhookDto>>;

public class CreateOutboundWebhookCommandHandler
    : IRequestHandler<CreateOutboundWebhookCommand, Result<OutboundWebhookDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateOutboundWebhookCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<OutboundWebhookDto>> Handle(
        CreateOutboundWebhookCommand request, CancellationToken ct)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
            return Result<OutboundWebhookDto>.Failure("Invalid webhook URL.", "400");

        var hook = new OutboundWebhookConfig
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            Url = request.Url,
            Events = request.Events,
            Secret = request.Secret,
            ProjectId = request.ProjectId,
            IsEnabled = true
        };

        await _uow.OutboundWebhooks.AddAsync(hook, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<OutboundWebhookDto>.Success(GetOutboundWebhooksQueryHandler.Map(hook));
    }
}

public record DeleteOutboundWebhookCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteOutboundWebhookCommandHandler
    : IRequestHandler<DeleteOutboundWebhookCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteOutboundWebhookCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DeleteOutboundWebhookCommand request, CancellationToken ct)
    {
        var hook = await _uow.OutboundWebhooks.GetByIdAsync(request.Id, ct);
        if (hook is null || hook.TenantId != _currentUser.TenantId)
            return Result<bool>.Failure("Webhook not found.", "404");

        await _uow.OutboundWebhooks.DeleteAsync(hook, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public record TestOutboundWebhookCommand(Guid Id) : IRequest<Result<string>>;

public class TestOutboundWebhookCommandHandler
    : IRequestHandler<TestOutboundWebhookCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpClientFactory _httpClientFactory;

    public TestOutboundWebhookCommandHandler(
        IUnitOfWork uow, ICurrentUser currentUser, IHttpClientFactory httpClientFactory)
    {
        _uow = uow;
        _currentUser = currentUser;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Result<string>> Handle(TestOutboundWebhookCommand request, CancellationToken ct)
    {
        var hook = await _uow.OutboundWebhooks.GetByIdAsync(request.Id, ct);
        if (hook is null || hook.TenantId != _currentUser.TenantId)
            return Result<string>.Failure("Webhook not found.", "404");

        var client = _httpClientFactory.CreateClient("notifications");
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            @event = "test",
            message = "DeployFlow webhook test",
            timestamp = DateTime.UtcNow
        });

        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(hook.Secret))
        {
            var hmac = new System.Security.Cryptography.HMACSHA256(
                System.Text.Encoding.UTF8.GetBytes(hook.Secret));
            var sig = Convert.ToHexString(hmac.ComputeHash(
                System.Text.Encoding.UTF8.GetBytes(payload))).ToLower();
            content.Headers.Add("X-DeployFlow-Signature", $"sha256={sig}");
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var resp = await client.PostAsync(hook.Url, content, cts.Token);
            hook.LastDeliveredAt = DateTime.UtcNow;
            hook.DeliveryCount++;
            hook.LastResponseStatus = ((int)resp.StatusCode).ToString();
            if (!resp.IsSuccessStatusCode) hook.FailureCount++;

            await _uow.OutboundWebhooks.UpdateAsync(hook, ct);
            await _uow.SaveChangesAsync(ct);

            return resp.IsSuccessStatusCode
                ? Result<string>.Success($"Test delivered. Response: {(int)resp.StatusCode}")
                : Result<string>.Failure($"Webhook returned {(int)resp.StatusCode}", "422");
        }
        catch (Exception ex)
        {
            hook.FailureCount++;
            hook.LastResponseStatus = "error";
            await _uow.OutboundWebhooks.UpdateAsync(hook, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<string>.Failure($"Delivery failed: {ex.Message}", "422");
        }
    }
}
