using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;
using Microsoft.Extensions.Options;

namespace DeployFlow.Application.Features.Deployments.Commands;

// ─── Trigger Deployment ───────────────────────────────────────────────────────

public record TriggerDeploymentCommand(
    Guid ProjectId,
    string? Branch,
    string? CommitSha,
    string Trigger = "manual",
    string? EnvironmentName = null
) : IRequest<Result<DeploymentDto>>;

public class TriggerDeploymentCommandValidator : AbstractValidator<TriggerDeploymentCommand>
{
    public TriggerDeploymentCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Trigger)
            .Must(t => new[] { "manual", "push", "schedule", "api", "rollback", "pipeline" }.Contains(t))
            .WithMessage("Trigger must be one of: manual, push, schedule, api, rollback, pipeline.");
    }
}

public class TriggerDeploymentCommandHandler : IRequestHandler<TriggerDeploymentCommand, Result<DeploymentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IGitService _gitService;
    private readonly FeatureFlagsOptions _featureFlags;

    public TriggerDeploymentCommandHandler(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IMapper mapper,
        IGitService gitService,
        IOptions<FeatureFlagsOptions> featureFlags)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
        _gitService = gitService;
        _featureFlags = featureFlags.Value;
    }

    public async Task<Result<DeploymentDto>> Handle(TriggerDeploymentCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null)
            return Result<DeploymentDto>.Failure("Project not found.", 404);

        // Webhook receivers are unauthenticated endpoints and may not have a populated current user context.
        // Keep tenant enforcement for authenticated requests, but allow system/webhook-triggered flows.
        if (_currentUser.TenantId != Guid.Empty && project.TenantId != _currentUser.TenantId)
            return Result<DeploymentDto>.Failure("Project not found.", 404);

        var branch = request.Branch ?? project.RepositoryBranch ?? "main";
        ProjectDeploymentEnvironment? branchMapping = null;
        if (_featureFlags.MultiEnvironmentEnabled)
        {
            var envMappings = await _uow.ProjectDeploymentEnvironments.GetByTenantAsync(_currentUser.TenantId, ct);
            var projectMappings = envMappings
                .Where(x => x.ProjectId == request.ProjectId)
                .OrderBy(x => x.Order)
                .ToList();

            if (!string.IsNullOrWhiteSpace(request.EnvironmentName))
            {
                branchMapping = projectMappings.FirstOrDefault(x =>
                    x.EnvironmentName.Equals(request.EnvironmentName, StringComparison.OrdinalIgnoreCase));
            }

            branchMapping ??= projectMappings.FirstOrDefault(x =>
                x.Branch.Equals(branch, StringComparison.OrdinalIgnoreCase));

            branchMapping ??= projectMappings.FirstOrDefault(x => x.IsDefault);

            if (request.Trigger.Equals("push", StringComparison.OrdinalIgnoreCase) &&
                branchMapping is not null &&
                !branchMapping.AutoDeploy)
            {
                return Result<DeploymentDto>.Failure(
                    $"Auto deploy is disabled for branch '{branch}' ({branchMapping.EnvironmentName}).", 409);
            }
        }

        string? commitSha = request.CommitSha;
        string? commitMessage = null;
        string? commitAuthor = null;

        if (string.IsNullOrEmpty(commitSha) && !string.IsNullOrEmpty(project.RepositoryUrl))
        {
            var commit = await _gitService.GetLatestCommitAsync(
                project.RepositoryUrl, branch, ct: ct);
            if (commit is not null)
            {
                commitSha = commit.Sha;
                commitMessage = commit.Message;
                commitAuthor = commit.Author;
            }
        }

        var triggerEnum = request.Trigger.ToLower() switch
        {
            "push" => DeploymentTrigger.GitPush,
            "schedule" => DeploymentTrigger.Schedule,
            "api" => DeploymentTrigger.Api,
            "pipeline" => DeploymentTrigger.Manual,
            _ => DeploymentTrigger.Manual
        };

        var deployment = Deployment.Create(
            tenantId: _currentUser.TenantId,
            projectId: request.ProjectId,
            trigger: triggerEnum,
            branch: branch,
            commitSha: commitSha,
            commitMessage: commitMessage,
            commitAuthor: commitAuthor,
            environmentId: project.EnvironmentId,
            serverId: project.ServerId,
            triggeredBy: _currentUser.UserId
        );

        var version = BuildVersionTag(project.Slug);
        deployment.SetVersion(version.Version);
        deployment.SetImageTag(version.ImageTag);
        deployment.Metadata["deploymentVersion"] = version.Version;
        deployment.Metadata["versionScheme"] = "build-date-seq";

        if (branchMapping is not null)
        {
            deployment.Metadata["environmentName"] = branchMapping.EnvironmentName;
            deployment.Metadata["environmentBranch"] = branchMapping.Branch;
            deployment.Metadata["environmentAutoDeploy"] = branchMapping.AutoDeploy.ToString().ToLowerInvariant();
            deployment.Metadata["environmentRequiresApproval"] = branchMapping.RequiresApproval.ToString().ToLowerInvariant();
            if (branchMapping.RequiresApproval)
                deployment.RequestApproval();
        }

        await _uow.Deployments.AddAsync(deployment, ct);
        project.RecordDeployment(deployment.Id, deployment.Status);
        await _uow.SaveChangesAsync(ct);

        return Result<DeploymentDto>.Success(_mapper.Map<DeploymentDto>(deployment));
    }

    private static (string Version, string ImageTag) BuildVersionTag(string projectSlug)
    {
        var utc = DateTime.UtcNow;
        var version = $"build-{utc:yyyyMMddHHmmss}";
        var imageTag = $"deployflow/{projectSlug}:{version}";
        return (version, imageTag);
    }
}

// ─── Re-run Failed Deployment ────────────────────────────────────────────────

public record RerunDeploymentCommand(Guid DeploymentId) : IRequest<Result<DeploymentDto>>;

public class RerunDeploymentCommandHandler : IRequestHandler<RerunDeploymentCommand, Result<DeploymentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public RerunDeploymentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<DeploymentDto>> Handle(RerunDeploymentCommand request, CancellationToken ct)
    {
        var previous = await _uow.Deployments.GetByIdAsync(request.DeploymentId, ct);
        if (previous is null || previous.TenantId != _currentUser.TenantId)
            return Result<DeploymentDto>.Failure("Deployment not found.", 404);

        if (previous.Status is not DeploymentStatus.Failed and not DeploymentStatus.Cancelled)
            return Result<DeploymentDto>.Failure("Only failed or cancelled deployments can be re-run.");

        var project = await _uow.Projects.GetByIdAsync(previous.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<DeploymentDto>.Failure("Project not found.", 404);

        var branch = previous.Branch ?? project.RepositoryBranch ?? "main";

        var rerun = Deployment.Create(
            tenantId: _currentUser.TenantId,
            projectId: previous.ProjectId,
            trigger: DeploymentTrigger.Manual,
            branch: branch,
            commitSha: previous.CommitSha,
            commitMessage: previous.CommitMessage,
            commitAuthor: previous.CommitAuthor,
            environmentId: previous.EnvironmentId,
            serverId: previous.ServerId,
            triggeredBy: _currentUser.UserId);

        await _uow.Deployments.AddAsync(rerun, ct);
        project.RecordDeployment(rerun.Id, rerun.Status);
        await _uow.SaveChangesAsync(ct);

        return Result<DeploymentDto>.Success(_mapper.Map<DeploymentDto>(rerun));
    }
}

// ─── Cancel Deployment ────────────────────────────────────────────────────────

public record CancelDeploymentCommand(Guid Id) : IRequest<Result>;

public class CancelDeploymentCommandHandler : IRequestHandler<CancelDeploymentCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CancelDeploymentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result> Handle(CancelDeploymentCommand request, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(request.Id, ct);
        if (deployment is null || deployment.TenantId != _currentUser.TenantId)
            return Result.Failure("Deployment not found.", 404);

        if (!deployment.CanBeCancelled)
            return Result.Failure("Deployment cannot be cancelled in its current state.");

        deployment.Cancel();
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Rollback Deployment ──────────────────────────────────────────────────────

public record RollbackDeploymentCommand(Guid DeploymentId) : IRequest<Result<DeploymentDto>>;

public class RollbackDeploymentCommandHandler : IRequestHandler<RollbackDeploymentCommand, Result<DeploymentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public RollbackDeploymentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    { _uow = uow; _currentUser = currentUser; _mapper = mapper; }

    public async Task<Result<DeploymentDto>> Handle(RollbackDeploymentCommand request, CancellationToken ct)
    {
        var current = await _uow.Deployments.GetByIdAsync(request.DeploymentId, ct);
        if (current is null || current.TenantId != _currentUser.TenantId)
            return Result<DeploymentDto>.Failure("Deployment not found.", 404);

        var candidates = await _uow.Deployments.GetByTenantAsync(_currentUser.TenantId, ct);
        var rollbackTarget = candidates
            .Where(d => d.ProjectId == current.ProjectId
                && d.Id != current.Id
                && !d.IsDeleted
                && (d.Status == DeploymentStatus.Healthy || d.Status == DeploymentStatus.Running)
                && d.CreatedAt < current.CreatedAt)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefault()
            ?? candidates
                .Where(d => d.ProjectId == current.ProjectId
                    && d.Id != current.Id
                    && !d.IsDeleted
                    && (d.Status == DeploymentStatus.Healthy || d.Status == DeploymentStatus.Running))
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefault();

        if (rollbackTarget is null)
            return Result<DeploymentDto>.Failure("No previous successful deployment found for rollback.", 409);

        var rollback = Deployment.CreateRollback(rollbackTarget, _currentUser.UserId);
        rollback.Metadata["rollbackRequestedFromDeploymentId"] = current.Id.ToString();
        await _uow.Deployments.AddAsync(rollback, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<DeploymentDto>.Success(_mapper.Map<DeploymentDto>(rollback));
    }
}

// ─── Approve Deployment ───────────────────────────────────────────────────────

public record ApproveDeploymentCommand(Guid Id, string? Notes) : IRequest<Result>;

public class ApproveDeploymentCommandHandler : IRequestHandler<ApproveDeploymentCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ApproveDeploymentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result> Handle(ApproveDeploymentCommand request, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(request.Id, ct);
        if (deployment is null || deployment.TenantId != _currentUser.TenantId)
            return Result.Failure("Deployment not found.", 404);

        try { deployment.Approve(_currentUser.UserId, request.Notes); }
        catch (InvalidOperationException ex) { return Result.Failure(ex.Message); }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Reject Deployment ────────────────────────────────────────────────────────

public record RejectDeploymentCommand(Guid Id, string? Notes) : IRequest<Result>;

public class RejectDeploymentCommandHandler : IRequestHandler<RejectDeploymentCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public RejectDeploymentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result> Handle(RejectDeploymentCommand request, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(request.Id, ct);
        if (deployment is null || deployment.TenantId != _currentUser.TenantId)
            return Result.Failure("Deployment not found.", 404);

        try { deployment.Reject(_currentUser.UserId, request.Notes); }
        catch (InvalidOperationException ex) { return Result.Failure(ex.Message); }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Start Canary ─────────────────────────────────────────────────────────────

public record StartCanaryCommand(Guid Id, int TrafficPercent, int StepDurationMinutes) : IRequest<Result>;

public class StartCanaryCommandHandler : IRequestHandler<StartCanaryCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public StartCanaryCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result> Handle(StartCanaryCommand request, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(request.Id, ct);
        if (deployment is null || deployment.TenantId != _currentUser.TenantId)
            return Result.Failure("Deployment not found.", 404);

        try { deployment.StartCanary(request.TrafficPercent, request.StepDurationMinutes); }
        catch (InvalidOperationException ex) { return Result.Failure(ex.Message); }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Promote Canary ───────────────────────────────────────────────────────────

public record PromoteCanaryCommand(Guid Id) : IRequest<Result>;

public class PromoteCanaryCommandHandler : IRequestHandler<PromoteCanaryCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public PromoteCanaryCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result> Handle(PromoteCanaryCommand request, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(request.Id, ct);
        if (deployment is null || deployment.TenantId != _currentUser.TenantId)
            return Result.Failure("Deployment not found.", 404);

        try { deployment.PromoteCanary(); }
        catch (InvalidOperationException ex) { return Result.Failure(ex.Message); }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Abort Canary ─────────────────────────────────────────────────────────────

public record AbortCanaryCommand(Guid Id) : IRequest<Result>;

public class AbortCanaryCommandHandler : IRequestHandler<AbortCanaryCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public AbortCanaryCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result> Handle(AbortCanaryCommand request, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(request.Id, ct);
        if (deployment is null || deployment.TenantId != _currentUser.TenantId)
            return Result.Failure("Deployment not found.", 404);

        try { deployment.AbortCanary(); }
        catch (InvalidOperationException ex) { return Result.Failure(ex.Message); }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Blue/Green Deploy Command ────────────────────────────────────────────────

/// <summary>
/// Triggers a Blue/Green deployment for a project.
/// Creates a regular Deployment record (trigger = Manual) then flags it for
/// blue/green processing in DeploymentRunnerService via the Metadata bag.
/// </summary>
public record BlueGreenDeployCommand(Guid ProjectId) : IRequest<Result<DeploymentDto>>;

public class BlueGreenDeployCommandHandler : IRequestHandler<BlueGreenDeployCommand, Result<DeploymentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public BlueGreenDeployCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    { _uow = uow; _currentUser = currentUser; _mapper = mapper; }

    public async Task<Result<DeploymentDto>> Handle(BlueGreenDeployCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<DeploymentDto>.Failure("Project not found.", 404);

        var deployment = Deployment.Create(
            tenantId: _currentUser.TenantId,
            projectId: project.Id,
            trigger: DeploymentTrigger.Manual,
            triggeredBy: _currentUser.UserId);

        // Mark as a blue/green deployment via metadata
        deployment.Metadata["strategy"] = "blue-green";
        deployment.Metadata["targetSlot"] = project.ActiveSlot == "blue" ? "green" : "blue";

        await _uow.Deployments.AddAsync(deployment, ct);
        project.RecordDeployment(deployment.Id, deployment.Status);
        await _uow.SaveChangesAsync(ct);

        return Result<DeploymentDto>.Success(_mapper.Map<DeploymentDto>(deployment));
    }
}
