using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;

namespace DeployFlow.Application.Features.Deployments.Commands;

// ─── Trigger Deployment ───────────────────────────────────────────────────────

public record TriggerDeploymentCommand(
    Guid ProjectId,
    string? Branch,
    string? CommitSha,
    string Trigger = "manual"
) : IRequest<Result<DeploymentDto>>;

public class TriggerDeploymentCommandValidator : AbstractValidator<TriggerDeploymentCommand>
{
    public TriggerDeploymentCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Trigger)
            .Must(t => new[] { "manual", "push", "schedule", "api", "rollback" }.Contains(t))
            .WithMessage("Trigger must be one of: manual, push, schedule, api, rollback.");
    }
}

public class TriggerDeploymentCommandHandler : IRequestHandler<TriggerDeploymentCommand, Result<DeploymentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IGitService _gitService;

    public TriggerDeploymentCommandHandler(
        IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper, IGitService gitService)
    { _uow = uow; _currentUser = currentUser; _mapper = mapper; _gitService = gitService; }

    public async Task<Result<DeploymentDto>> Handle(TriggerDeploymentCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<DeploymentDto>.Failure("Project not found.", 404);

        var branch = request.Branch ?? project.RepositoryBranch ?? "main";
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
            triggeredBy: _currentUser.UserId
        );

        await _uow.Deployments.AddAsync(deployment, ct);
        project.RecordDeployment(deployment.Id, deployment.Status);
        await _uow.SaveChangesAsync(ct);

        return Result<DeploymentDto>.Success(_mapper.Map<DeploymentDto>(deployment));
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
        var original = await _uow.Deployments.GetByIdAsync(request.DeploymentId, ct);
        if (original is null || original.TenantId != _currentUser.TenantId)
            return Result<DeploymentDto>.Failure("Deployment not found.", 404);

        if (original.Status != DeploymentStatus.Healthy && original.Status != DeploymentStatus.Running)
            return Result<DeploymentDto>.Failure("Can only roll back to a healthy or running deployment.");

        var rollback = Deployment.CreateRollback(original, _currentUser.UserId);
        await _uow.Deployments.AddAsync(rollback, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<DeploymentDto>.Success(_mapper.Map<DeploymentDto>(rollback));
    }
}
