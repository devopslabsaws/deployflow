using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using System.Text.RegularExpressions;

namespace DeployFlow.Application.Features.AI;

public record AiTimelineEventDto(DateTime At, string Title, string Detail, string Tone);
public record AiActionPlanDto(string Label, string Detail, string ActionType, string RiskLevel, string? Command);

public record AiIncidentCommanderDto(
    string DeploymentId,
    string ProjectId,
    string ProjectName,
    string Severity,
    int Confidence,
    string Summary,
    string LikelyCause,
    string BlastRadius,
    string RecommendedDecision,
    string? SloStatus,
    IReadOnlyList<AiTimelineEventDto> Timeline,
    IReadOnlyList<AiActionPlanDto> ActionPlan,
    IReadOnlyList<string> ActiveSignals,
    DateTime GeneratedAt);

public record AiRiskFactorDto(string Name, string Category, int Impact, string Detail);

public record AiRiskAssessmentDto(
    string ProjectId,
    string ProjectName,
    string Branch,
    string Environment,
    int Score,
    string Level,
    string Verdict,
    string Summary,
    string SuggestedRollout,
    IReadOnlyList<AiRiskFactorDto> Factors,
    IReadOnlyList<string> RecommendedActions,
    DateTime GeneratedAt);

public record AiPolicyDraftDto(
    string Name,
    string Description,
    string AppliesTo,
    int RequiredApprovals,
    string? RequiredApproverRole,
    string? AutoApprovePattern,
    string? AllowedHoursUtc,
    bool IsEnabled);

public record AiPolicySimulationDto(
    string Prompt,
    string ParsedIntent,
    int ImpactedProjectCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<AiPolicyDraftDto> Drafts,
    DateTime GeneratedAt);

public record AiPreviewQaCheckDto(string Area, string Step, string Priority, string Rationale);

public record AiPreviewQaPlanDto(
    string ProjectId,
    string ProjectName,
    string Branch,
    string PrTitle,
    string PreviewStrategy,
    string CommentBody,
    IReadOnlyList<AiPreviewQaCheckDto> Checks,
    IReadOnlyList<string> FocusAreas,
    DateTime GeneratedAt);

public record AiPipelineBlueprintDto(
    string Name,
    string Summary,
    string RolloutMode,
    IReadOnlyList<string> Stages,
    IReadOnlyList<string> Benefits,
    IReadOnlyList<string> Tradeoffs);

public record AiPipelineArchitectureDto(
    string ProjectId,
    string ProjectName,
    string Framework,
    string Recommendation,
    IReadOnlyList<AiPipelineBlueprintDto> Blueprints,
    DateTime GeneratedAt);

public record AiOpsMemoryItemDto(
    string ProjectId,
    string ProjectName,
    string Pattern,
    string Severity,
    int Occurrences,
    DateTime LastSeenAt,
    string RecommendedFocus);

public record AiOpsMemoryDto(
    string Summary,
    IReadOnlyList<AiOpsMemoryItemDto> Items,
    DateTime GeneratedAt);

public record GetAiIncidentCommanderQuery(Guid DeploymentId) : IRequest<Result<AiIncidentCommanderDto>>;
public record AssessDeploymentRiskCommand(Guid ProjectId, string? Branch, string? EnvironmentSlug) : IRequest<Result<AiRiskAssessmentDto>>;
public record SimulatePolicyFromPromptCommand(string Prompt) : IRequest<Result<AiPolicySimulationDto>>;
public record GeneratePreviewQaPlanCommand(Guid ProjectId, string Branch, string PrTitle, string? ChangeSummary) : IRequest<Result<AiPreviewQaPlanDto>>;
public record GeneratePipelineArchitectureQuery(Guid ProjectId) : IRequest<Result<AiPipelineArchitectureDto>>;
public record GetAiOpsMemoryQuery(Guid? ProjectId = null) : IRequest<Result<AiOpsMemoryDto>>;

public class GetAiIncidentCommanderQueryHandler : IRequestHandler<GetAiIncidentCommanderQuery, Result<AiIncidentCommanderDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;

    public GetAiIncidentCommanderQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, ISender sender)
    {
        _uow = uow;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<Result<AiIncidentCommanderDto>> Handle(GetAiIncidentCommanderQuery request, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(request.DeploymentId, ct);
        if (deployment is null || deployment.TenantId != _currentUser.TenantId)
            return Result<AiIncidentCommanderDto>.Failure("Deployment not found.", "404");

        var project = await _uow.Projects.GetByIdAsync(deployment.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<AiIncidentCommanderDto>.Failure("Project not found.", "404");

        var analysisResult = await _sender.Send(new AnalyzeDeploymentQuery(request.DeploymentId), ct);
        if (!analysisResult.IsSuccess || analysisResult.Value is null)
            return Result<AiIncidentCommanderDto>.Failure("AI analysis unavailable.");

        var analysis = analysisResult.Value;
        var (logs, _) = await _uow.Deployments.GetLogsPagedAsync(request.DeploymentId, 1, 200, ct);
        var firstError = logs.FirstOrDefault(x => x.Level == LogLevel.Error);
        var activeAlerts = await _uow.Alerts.GetPagedAsync(_currentUser.TenantId, 1, 20, acknowledged: false, ct: ct);
        var relevantAlerts = activeAlerts.Items
            .Where(a => a.ResourceId == deployment.ProjectId || a.ResourceId == deployment.ServerId || string.Equals(a.ResourceType, "project", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToList();

        var slo = (await _uow.ProjectSlos.GetByTenantAsync(_currentUser.TenantId, ct))
            .FirstOrDefault(x => x.ProjectId == project.Id && x.IsEnabled);
        var environments = await _uow.Environments.GetByTenantAsync(_currentUser.TenantId, ct);
        var environment = project.EnvironmentId.HasValue
            ? environments.FirstOrDefault(x => x.Id == project.EnvironmentId.Value)
            : null;

        var timeline = new List<AiTimelineEventDto>();
        if (deployment.StartedAt.HasValue)
            timeline.Add(new AiTimelineEventDto(deployment.StartedAt.Value, "Deployment started", $"{project.Name} on branch {deployment.Branch ?? project.RepositoryBranch ?? "main"}", "info"));
        if (firstError is not null)
            timeline.Add(new AiTimelineEventDto(firstError.Timestamp, "First error captured", firstError.Message, "danger"));
        foreach (var alert in relevantAlerts)
            timeline.Add(new AiTimelineEventDto(alert.TriggeredAt, $"Alert: {alert.Name}", alert.Description ?? alert.Condition ?? alert.Source, alert.Severity.ToString().ToLowerInvariant()));
        if (deployment.FinishedAt.HasValue)
            timeline.Add(new AiTimelineEventDto(deployment.FinishedAt.Value, "Deployment finished", $"Status: {deployment.Status}", deployment.Status == DeploymentStatus.Failed ? "danger" : "success"));
        timeline = timeline.OrderBy(x => x.At).ToList();

        var actionPlan = new List<AiActionPlanDto>();
        foreach (var fix in analysis.AutoFixes.Take(3))
        {
            actionPlan.Add(new AiActionPlanDto(
                fix.Label,
                fix.Destructive ? "Approval recommended before execution." : "Safe candidate for guided retry.",
                "self-heal",
                fix.Destructive ? "high" : "medium",
                fix.Command));
        }

        foreach (var suggestion in analysis.Suggestions.Take(4))
        {
            actionPlan.Add(new AiActionPlanDto(
                suggestion.Title,
                suggestion.Detail,
                suggestion.Category,
                analysis.Severity is "critical" or "high" ? "high" : "medium",
                null));
        }

        if (!actionPlan.Any())
        {
            actionPlan.Add(new AiActionPlanDto(
                "Export incident packet",
                "Share the incident report with the team and attach it to the PR or ticket.",
                "coordination",
                "low",
                null));
        }

        var activeSignals = new List<string>();
        if (relevantAlerts.Any()) activeSignals.Add($"{relevantAlerts.Count} active alerts linked to this workload");
        if (slo?.CurrentUptimePercent is not null && slo.CurrentUptimePercent < slo.UptimeTargetPercent)
            activeSignals.Add($"SLO breach: uptime {slo.CurrentUptimePercent:0.00}% vs target {slo.UptimeTargetPercent:0.00}%");
        if (analysis.Severity is "critical" or "high") activeSignals.Add($"AI severity flagged as {analysis.Severity}");
        if (!string.IsNullOrWhiteSpace(deployment.ErrorMessage)) activeSignals.Add("Stored deployment error message available");

        var blastRadius = ResolveBlastRadius(environment?.IsProduction == true, relevantAlerts, slo, analysis.Severity);
        var confidence = analysis.Severity switch
        {
            "critical" => 94,
            "high" => 86,
            "medium" => 72,
            "low" => 60,
            _ => 55,
        };
        var decision = analysis.Severity is "critical"
            ? "Pause further promotions, review rollback/canary options, and apply only approval-gated self-heal actions."
            : analysis.Severity is "high"
                ? "Run guided remediation and preflight before redeploying."
                : "Proceed with targeted fix verification and monitor closely.";

        return Result<AiIncidentCommanderDto>.Success(new AiIncidentCommanderDto(
            deployment.Id.ToString(),
            project.Id.ToString(),
            project.Name,
            analysis.Severity,
            confidence,
            analysis.Diagnosis,
            analysis.RootCause,
            blastRadius,
            decision,
            slo is null ? null : (slo.CurrentUptimePercent.HasValue && slo.CurrentUptimePercent.Value < slo.UptimeTargetPercent ? "breaching" : "healthy"),
            timeline,
            actionPlan,
            activeSignals,
            DateTime.UtcNow));
    }

    private static string ResolveBlastRadius(bool production, IReadOnlyCollection<Alert> alerts, ProjectSlo? slo, string severity)
    {
        if (production || alerts.Any(a => a.Severity == AlertSeverity.Critical) || (slo?.CurrentUptimePercent.HasValue == true && slo.CurrentUptimePercent.Value < slo.UptimeTargetPercent))
            return "high";
        if (severity is "critical" or "high" || alerts.Any())
            return "medium";
        return "low";
    }
}

public class AssessDeploymentRiskCommandHandler : IRequestHandler<AssessDeploymentRiskCommand, Result<AiRiskAssessmentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public AssessDeploymentRiskCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<AiRiskAssessmentDto>> Handle(AssessDeploymentRiskCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<AiRiskAssessmentDto>.Failure("Project not found.", "404");

        var environments = await _uow.Environments.GetByTenantAsync(_currentUser.TenantId, ct);
        var environment = request.EnvironmentSlug
            ?? environments.FirstOrDefault(x => x.Id == project.EnvironmentId)?.Slug
            ?? "production";

        var (recentDeployments, _) = await _uow.Deployments.GetPagedAsync(_currentUser.TenantId, 1, 8, projectId: project.Id, ct: ct);
        var alerts = await _uow.Alerts.GetPagedAsync(_currentUser.TenantId, 1, 20, acknowledged: false, ct: ct);
        var projectAlerts = alerts.Items.Where(x => x.ResourceId == project.Id).ToList();
        var enabledPolicies = (await _uow.PolicyTemplates.GetByTenantAsync(_currentUser.TenantId, ct))
            .Where(x => x.IsEnabled && (x.ProjectId is null || x.ProjectId == project.Id))
            .ToList();
        var slo = (await _uow.ProjectSlos.GetByTenantAsync(_currentUser.TenantId, ct))
            .FirstOrDefault(x => x.ProjectId == project.Id && x.IsEnabled);

        var factors = new List<AiRiskFactorDto>();
        int score = environment.Equals("production", StringComparison.OrdinalIgnoreCase) ? 40 : 18;
        var branch = string.IsNullOrWhiteSpace(request.Branch) ? (project.RepositoryBranch ?? "main") : request.Branch.Trim();

        if (recentDeployments.Any())
        {
            var failedCount = recentDeployments.Count(x => x.Status == DeploymentStatus.Failed);
            if (failedCount > 0)
            {
                var impact = Math.Min(24, failedCount * 8);
                score += impact;
                factors.Add(new AiRiskFactorDto("Recent failures", "history", impact, $"{failedCount} of the last {recentDeployments.Count} deploys failed."));
            }

            if (recentDeployments.First().Status == DeploymentStatus.Failed)
            {
                score += 14;
                factors.Add(new AiRiskFactorDto("Last deploy failed", "history", 14, "The most recent deployment ended in failure."));
            }
        }

        if (projectAlerts.Any())
        {
            var impact = projectAlerts.Any(x => x.Severity == AlertSeverity.Critical) ? 18 : 10;
            score += impact;
            factors.Add(new AiRiskFactorDto("Open alerts", "operations", impact, $"{projectAlerts.Count} active alerts are still open for this project."));
        }

        if (slo is null)
        {
            score += 8;
            factors.Add(new AiRiskFactorDto("Missing SLO", "safety", 8, "No SLO is configured, so blast radius is harder to estimate."));
        }
        else if (slo.CurrentUptimePercent.HasValue && slo.CurrentUptimePercent.Value < slo.UptimeTargetPercent)
        {
            score += 16;
            factors.Add(new AiRiskFactorDto("SLO breach", "reliability", 16, $"Current uptime {slo.CurrentUptimePercent:0.00}% is under target {slo.UptimeTargetPercent:0.00}%."));
        }

        if (!project.ServerId.HasValue)
        {
            score += 20;
            factors.Add(new AiRiskFactorDto("No server assignment", "infra", 20, "Project is not attached to a deployment server."));
        }

        if (string.IsNullOrWhiteSpace(project.HealthCheckPath))
        {
            score += 8;
            factors.Add(new AiRiskFactorDto("Missing health check", "verification", 8, "Health check path is not configured."));
        }

        if (!enabledPolicies.Any() && environment.Equals("production", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            factors.Add(new AiRiskFactorDto("No approval policy", "governance", 10, "Production deploy has no active approval template."));
        }

        if (Regex.IsMatch(branch, "hotfix|release", RegexOptions.IgnoreCase))
        {
            score -= 4;
            factors.Add(new AiRiskFactorDto("Controlled branch", "branch", -4, $"Branch `{branch}` looks like a managed release or hotfix branch."));
        }

        score = Math.Clamp(score, 0, 100);
        var level = score >= 75 ? "high" : score >= 45 ? "medium" : "low";
        var verdict = score >= 75 ? "block" : score >= 45 ? "watch" : "safe";
        var rollout = score >= 75
            ? "Blue/Green with approval gate and preflight"
            : score >= 45
                ? "Canary with SLO watch window"
                : "Direct deploy with preflight and health check";

        var actions = new List<string>
        {
            "Run preflight before deploy.",
            verdict == "block" ? "Require manual approval before execution." : "Keep approval optional but visible.",
            rollout.Contains("Canary", StringComparison.OrdinalIgnoreCase) || rollout.Contains("Blue/Green", StringComparison.OrdinalIgnoreCase)
                ? "Use progressive rollout instead of direct cutover."
                : "Proceed with standard rollout and monitor live metrics.",
        };

        if (slo is null) actions.Add("Create an SLO for this project before the next production change.");
        if (string.IsNullOrWhiteSpace(project.HealthCheckPath)) actions.Add("Configure a health check path to allow automated verification.");

        return Result<AiRiskAssessmentDto>.Success(new AiRiskAssessmentDto(
            project.Id.ToString(),
            project.Name,
            branch,
            environment,
            score,
            level,
            verdict,
            $"Risk twin rated this change as {level} risk with a {verdict} verdict for {environment}.",
            rollout,
            factors.OrderByDescending(x => x.Impact).ToList(),
            actions,
            DateTime.UtcNow));
    }
}

public class SimulatePolicyFromPromptCommandHandler : IRequestHandler<SimulatePolicyFromPromptCommand, Result<AiPolicySimulationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public SimulatePolicyFromPromptCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<AiPolicySimulationDto>> Handle(SimulatePolicyFromPromptCommand request, CancellationToken ct)
    {
        var prompt = request.Prompt.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
            return Result<AiPolicySimulationDto>.Failure("Prompt is required.");

        var lower = prompt.ToLowerInvariant();
        var appliesTo = new List<string>();
        if (lower.Contains("production") || lower.Contains("prod")) appliesTo.Add("production");
        if (lower.Contains("staging")) appliesTo.Add("staging");
        if (lower.Contains("qa")) appliesTo.Add("qa");
        if (lower.Contains("dev")) appliesTo.Add("dev");
        if (!appliesTo.Any()) appliesTo.Add("production");

        var approvals = ParseApprovalCount(lower);
        var role = lower.Contains("admin") ? "admin"
            : lower.Contains("developer") ? "developer"
            : lower.Contains("viewer") ? "viewer"
            : null;
        var autoApprovePattern = lower.Contains("hotfix") ? "^hotfix/" : lower.Contains("release/") ? "^release/" : null;
        var allowedHoursUtc = lower.Contains("business hours") ? "09:00-17:00"
            : lower.Contains("after 6") || lower.Contains("after 18") ? "00:00-18:00"
            : null;

        var draft = new AiPolicyDraftDto(
            Name: BuildPolicyName(appliesTo, approvals),
            Description: prompt,
            AppliesTo: string.Join(",", appliesTo.Distinct()),
            RequiredApprovals: approvals,
            RequiredApproverRole: role,
            AutoApprovePattern: autoApprovePattern,
            AllowedHoursUtc: allowedHoursUtc,
            IsEnabled: true);

        var envs = await _uow.Environments.GetByTenantAsync(_currentUser.TenantId, ct);
        var projects = await _uow.Projects.GetByTenantAsync(_currentUser.TenantId, ct);
        var matchedEnvIds = envs.Where(x => appliesTo.Contains(x.Slug, StringComparer.OrdinalIgnoreCase)).Select(x => x.Id).ToHashSet();
        var impacted = projects.Count(x => x.EnvironmentId.HasValue && matchedEnvIds.Contains(x.EnvironmentId.Value));

        var warnings = new List<string>();
        if (allowedHoursUtc is not null && autoApprovePattern is not null)
            warnings.Add("Auto-approve branch patterns can bypass part of the approval friction. Review carefully for production use.");
        if (impacted == 0)
            warnings.Add("No existing projects matched the target environments. The policy will only affect future assignments.");
        if (role is null)
            warnings.Add("No approver role was inferred. Any authorized approver may satisfy this policy.");

        return Result<AiPolicySimulationDto>.Success(new AiPolicySimulationDto(
            prompt,
            $"Require {approvals} approval(s) for {string.Join(", ", appliesTo)} deployments"
                + (role is null ? "" : $", approved by role `{role}`")
                + (allowedHoursUtc is null ? "" : $", within {allowedHoursUtc} UTC")
                + (autoApprovePattern is null ? "" : $", auto-approved for `{autoApprovePattern}` branches"),
            impacted,
            warnings,
            new[] { draft },
            DateTime.UtcNow));
    }

    private static int ParseApprovalCount(string lower)
    {
        var digitMatch = Regex.Match(lower, @"(\d+)\s+approval");
        if (digitMatch.Success && int.TryParse(digitMatch.Groups[1].Value, out var count))
            return Math.Clamp(count, 1, 5);
        if (lower.Contains("two approval") || lower.Contains("2 approval")) return 2;
        if (lower.Contains("three approval") || lower.Contains("3 approval")) return 3;
        return 1;
    }

    private static string BuildPolicyName(IReadOnlyList<string> appliesTo, int approvals)
        => $"{Culture(appliesTo.First())} {approvals}-Approval Policy";

    private static string Culture(string value)
        => string.IsNullOrWhiteSpace(value) ? "Production" : char.ToUpperInvariant(value[0]) + value[1..];
}

public class GeneratePreviewQaPlanCommandHandler : IRequestHandler<GeneratePreviewQaPlanCommand, Result<AiPreviewQaPlanDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GeneratePreviewQaPlanCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<AiPreviewQaPlanDto>> Handle(GeneratePreviewQaPlanCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<AiPreviewQaPlanDto>.Failure("Project not found.", "404");

        var text = $"{request.PrTitle} {request.ChangeSummary} {project.Framework}".ToLowerInvariant();
        var focusAreas = new List<string>();
        if (text.Contains("auth") || text.Contains("login")) focusAreas.Add("authentication");
        if (text.Contains("payment") || text.Contains("checkout")) focusAreas.Add("payments");
        if (text.Contains("api") || text.Contains("endpoint")) focusAreas.Add("api contracts");
        if (text.Contains("ui") || text.Contains("page") || text.Contains("layout")) focusAreas.Add("responsive UI");
        if (text.Contains("deploy") || text.Contains("infra")) focusAreas.Add("runtime health");
        if (!focusAreas.Any()) focusAreas.AddRange(new[] { "core smoke path", "runtime health", "error states" });

        var checks = new List<AiPreviewQaCheckDto>
        {
            new("Smoke", "Open the preview URL and verify the application boots without blank screens or console-blocking errors.", "high", "Every PR preview should pass a cold-start smoke test."),
            new("Health", "Confirm health endpoint or main landing page returns success within the expected startup window.", "high", "Previews that start slowly often hide deploy regressions."),
        };

        foreach (var area in focusAreas.Distinct())
        {
            checks.Add(area switch
            {
                "authentication" => new AiPreviewQaCheckDto("Authentication", "Verify login, logout, and session refresh flows with both valid and invalid credentials.", "high", "Auth regressions block most users immediately."),
                "payments" => new AiPreviewQaCheckDto("Payments", "Run checkout in test mode, confirm totals, retries, and decline messaging.", "high", "Payment regressions have direct revenue impact."),
                "api contracts" => new AiPreviewQaCheckDto("API", "Inspect changed endpoints for shape changes, status codes, and error payload consistency.", "medium", "Contract drift breaks frontend or external consumers."),
                "responsive UI" => new AiPreviewQaCheckDto("UI", "Review the changed screens on mobile and desktop breakpoints for overflow, clipping, and focus order.", "medium", "Preview QA should catch visual regressions before merge."),
                _ => new AiPreviewQaCheckDto("Runtime", "Inspect logs for startup warnings, missing env vars, or flaky dependency calls.", "medium", "Operational issues often appear before user-facing bugs.")
            });
        }

        var strategy = project.PreviewDeployEnabled
            ? "Use project preview environment and keep it alive until PR decision."
            : "Use ephemeral environment with a 24h TTL and capture QA evidence in the PR.";

        var commentBody = string.Join("\n", new[]
        {
            "### DeployFlow Preview QA Plan",
            $"- Project: {project.Name}",
            $"- Branch: {request.Branch}",
            $"- PR: {request.PrTitle}",
            $"- Strategy: {strategy}",
            "",
            "#### Recommended checks",
        }.Concat(checks.Select((x, i) => $"{i + 1}. [{x.Priority.ToUpperInvariant()}] {x.Step}")));

        return Result<AiPreviewQaPlanDto>.Success(new AiPreviewQaPlanDto(
            project.Id.ToString(),
            project.Name,
            request.Branch,
            request.PrTitle,
            strategy,
            commentBody,
            checks,
            focusAreas.Distinct().ToList(),
            DateTime.UtcNow));
    }
}

public class GeneratePipelineArchitectureQueryHandler : IRequestHandler<GeneratePipelineArchitectureQuery, Result<AiPipelineArchitectureDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GeneratePipelineArchitectureQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<AiPipelineArchitectureDto>> Handle(GeneratePipelineArchitectureQuery request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<AiPipelineArchitectureDto>.Failure("Project not found.", "404");

        var framework = string.IsNullOrWhiteSpace(project.Framework) ? "application" : project.Framework;
        var blueprints = new List<AiPipelineBlueprintDto>
        {
            new(
                "Fast Feedback",
                "Optimize for quick developer confidence on every branch push.",
                "Direct deploy after tests",
                new[] { "Setup with dependency cache", "Build", "Parallel tests", "Preview deploy", "Smoke verify" },
                new[] { "Shortest feedback loop", "Good for feature branches", "Cheap to operate" },
                new[] { "Lower safety margin for production", "Relies on strong test coverage" }),
            new(
                "Safe Production",
                "Optimize for controlled rollout and easy reversibility.",
                "Approval + canary or blue/green",
                new[] { "Preflight", "Build", "Tests", "Approval gate", "Canary or Blue/Green", "SLO verification", "Incident export on failure" },
                new[] { "Best production safety", "Works well with approval policies", "Clear rollback posture" },
                new[] { "Longer lead time", "More operator input required" }),
            new(
                "Cost Aware",
                "Optimize for lower build and runtime spend while preserving observability.",
                "Cached build with scheduled cleanup",
                new[] { "Setup with aggressive caching", "Selective tests", "Docker layer reuse", "Deploy", "Image cleanup", "Metrics emit" },
                new[] { "Lower CI minutes", "Smaller image churn", "Good for high-frequency repos" },
                new[] { "May miss broader regressions if test selection is too narrow" }),
        };

        var recommendation = project.LastDeploymentStatus == DeploymentStatus.Failed || project.BranchDeployEnabled || project.PreviewDeployEnabled
            ? "Safe Production"
            : project.AutoDeployEnabled
                ? "Fast Feedback"
                : "Cost Aware";

        return Result<AiPipelineArchitectureDto>.Success(new AiPipelineArchitectureDto(
            project.Id.ToString(),
            project.Name,
            framework,
            $"Recommended blueprint: {recommendation} for {framework} workloads in the current project state.",
            blueprints,
            DateTime.UtcNow));
    }
}

public class GetAiOpsMemoryQueryHandler : IRequestHandler<GetAiOpsMemoryQuery, Result<AiOpsMemoryDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;

    public GetAiOpsMemoryQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, ISender sender)
    {
        _uow = uow;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<Result<AiOpsMemoryDto>> Handle(GetAiOpsMemoryQuery request, CancellationToken ct)
    {
        var (deployments, _) = await _uow.Deployments.GetPagedAsync(_currentUser.TenantId, 1, 20, projectId: request.ProjectId, status: "failed", ct: ct);
        var projects = await _uow.Projects.GetByTenantAsync(_currentUser.TenantId, ct);
        var projectMap = projects.ToDictionary(x => x.Id, x => x.Name);
        var items = new List<AiOpsMemoryItemDto>();

        foreach (var deployment in deployments.Take(12))
        {
            var analysis = await _sender.Send(new AnalyzeDeploymentQuery(deployment.Id), ct);
            var pattern = analysis.IsSuccess && analysis.Value is not null ? analysis.Value.Diagnosis : (deployment.ErrorMessage ?? "Unknown deployment failure");
            items.Add(new AiOpsMemoryItemDto(
                deployment.ProjectId.ToString(),
                projectMap.GetValueOrDefault(deployment.ProjectId, "Unknown"),
                pattern,
                analysis.Value?.Severity ?? "medium",
                1,
                deployment.FinishedAt ?? deployment.StartedAt ?? deployment.CreatedAt,
                analysis.Value?.Suggestions.FirstOrDefault()?.Title ?? "Review the first failing log line and capture a fix recipe."));
        }

        var grouped = items
            .GroupBy(x => new { x.ProjectId, x.ProjectName, x.Pattern, x.Severity, x.RecommendedFocus })
            .Select(g => new AiOpsMemoryItemDto(
                g.Key.ProjectId,
                g.Key.ProjectName,
                g.Key.Pattern,
                g.Key.Severity,
                g.Count(),
                g.Max(x => x.LastSeenAt),
                g.Key.RecommendedFocus))
            .OrderByDescending(x => x.Occurrences)
            .ThenByDescending(x => x.LastSeenAt)
            .Take(8)
            .ToList();

        var summary = grouped.Count == 0
            ? "No repeated failure patterns detected in recent deployments."
            : $"Detected {grouped.Count} recurring failure pattern(s) across recent deployments.";

        return Result<AiOpsMemoryDto>.Success(new AiOpsMemoryDto(summary, grouped, DateTime.UtcNow));
    }
}