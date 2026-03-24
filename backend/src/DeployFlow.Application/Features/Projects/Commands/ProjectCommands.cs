using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;

namespace DeployFlow.Application.Features.Projects.Commands;

// ─── Create Project ───────────────────────────────────────────────────────────

public record CreateProjectCommand(
    string Name,
    string? Description,
    string? RepositoryUrl,
    string? Branch,
    string? BuildCommand,
    string? StartCommand,
    string? InstallCommand,
    string? DockerfilePath,
    string? Framework,
    string? CustomDomain,
    bool AutoDeploy,
    string[] Tags,
    Guid? AssignedServerId
) : IRequest<Result<ProjectDto>>;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9\s\-_\.]+$").WithMessage("Name contains invalid characters.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.RepositoryUrl)
            .Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Repository URL must be a valid URL.");
        RuleFor(x => x.CustomDomain)
            .Must(d => string.IsNullOrEmpty(d) ||
                System.Text.RegularExpressions.Regex.IsMatch(d, @"^[a-zA-Z0-9][a-zA-Z0-9\-\.]+[a-zA-Z0-9]$"))
            .WithMessage("Custom domain is not valid.");
    }
}

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Result<ProjectDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public CreateProjectCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    { _uow = uow; _currentUser = currentUser; _mapper = mapper; }

    public async Task<Result<ProjectDto>> Handle(CreateProjectCommand request, CancellationToken ct)
    {
        var existing = await _uow.Projects.GetByNameAsync(request.Name, _currentUser.TenantId, ct);
        if (existing is not null)
            return Result<ProjectDto>.Failure("A project with this name already exists.");

        var project = Domain.Entities.Project.Create(
            tenantId: _currentUser.TenantId,
            name: request.Name,
            description: request.Description,
            repositoryUrl: request.RepositoryUrl,
            repositoryBranch: request.Branch ?? "main",
            buildCommand: request.BuildCommand,
            startCommand: request.StartCommand,
            installCommand: request.InstallCommand,
            dockerfilePath: request.DockerfilePath,
            framework: request.Framework,
            customDomain: request.CustomDomain,
            autoDeployEnabled: request.AutoDeploy,
            tags: request.Tags,
            createdBy: _currentUser.UserId
        );

        if (request.AssignedServerId.HasValue)
            project.AssignServer(request.AssignedServerId.Value);

        await _uow.Projects.AddAsync(project, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ProjectDto>.Success(_mapper.Map<ProjectDto>(project));
    }
}

// ─── Update Project ───────────────────────────────────────────────────────────

public record UpdateProjectCommand(
    Guid Id,
    string? Name,
    string? Description,
    string? RepositoryUrl,
    string? Branch,
    string? BuildCommand,
    string? StartCommand,
    string? InstallCommand,
    string? DockerfilePath,
    string? Framework,
    string? CustomDomain,
    bool? AutoDeploy,
    string[]? Tags,
    Guid? AssignedServerId
) : IRequest<Result<ProjectDto>>;

public class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9\s\-_\.]+$").When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, Result<ProjectDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateProjectCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    { _uow = uow; _currentUser = currentUser; _mapper = mapper; }

    public async Task<Result<ProjectDto>> Handle(UpdateProjectCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.Id, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<ProjectDto>.Failure("Project not found.", 404);

        project.Update(
            name: request.Name,
            description: request.Description,
            repositoryUrl: request.RepositoryUrl,
            repositoryBranch: request.Branch,
            buildCommand: request.BuildCommand,
            startCommand: request.StartCommand,
            installCommand: request.InstallCommand,
            dockerfilePath: request.DockerfilePath,
            framework: request.Framework,
            customDomain: request.CustomDomain,
            autoDeployEnabled: request.AutoDeploy,
            updatedBy: _currentUser.UserId
        );

        if (request.Tags is not null) project.AddTags(request.Tags);
        if (request.AssignedServerId.HasValue) project.AssignServer(request.AssignedServerId.Value);

        await _uow.SaveChangesAsync(ct);
        return Result<ProjectDto>.Success(_mapper.Map<ProjectDto>(project));
    }
}

// ─── Delete Project ───────────────────────────────────────────────────────────

public record DeleteProjectCommand(Guid Id) : IRequest<Result>;

public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteProjectCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result> Handle(DeleteProjectCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.Id, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result.Failure("Project not found.", 404);

        project.Archive();
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Clone Project ────────────────────────────────────────────────────────────

public record CloneProjectCommand(Guid SourceId, string? NewName) : IRequest<Result<ProjectDto>>;

public class CloneProjectCommandHandler : IRequestHandler<CloneProjectCommand, Result<ProjectDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public CloneProjectCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    { _uow = uow; _currentUser = currentUser; _mapper = mapper; }

    public async Task<Result<ProjectDto>> Handle(CloneProjectCommand request, CancellationToken ct)
    {
        var src = await _uow.Projects.GetByIdAsync(request.SourceId, ct);
        if (src is null || src.TenantId != _currentUser.TenantId)
            return Result<ProjectDto>.Failure("Source project not found.", 404);

        var cloneName = !string.IsNullOrWhiteSpace(request.NewName)
            ? request.NewName.Trim()
            : $"{src.Name} (Clone)";

        // Ensure the name is unique — append a timestamp if taken
        var existing = await _uow.Projects.GetByNameAsync(cloneName, _currentUser.TenantId, ct);
        if (existing is not null)
            cloneName = $"{cloneName} {DateTime.UtcNow:HHmmss}";

        var clone = Domain.Entities.Project.Create(
            tenantId:          _currentUser.TenantId,
            name:              cloneName,
            description:       src.Description,
            repositoryUrl:     src.RepositoryUrl,
            repositoryBranch:  src.RepositoryBranch ?? "main",
            buildCommand:      src.BuildCommand,
            startCommand:      src.StartCommand,
            installCommand:    src.InstallCommand,
            dockerfilePath:    src.DockerfilePath,
            framework:         src.Framework,
            customDomain:      null,   // custom domain must be unique — not copied
            autoDeployEnabled: src.AutoDeployEnabled,
            tags:              src.Tags?.ToArray() ?? [],
            createdBy:         _currentUser.UserId
        );

        if (src.ServerId.HasValue)
            clone.AssignServer(src.ServerId.Value);

        await _uow.Projects.AddAsync(clone, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ProjectDto>.Success(_mapper.Map<ProjectDto>(clone));
    }
}
