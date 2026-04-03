using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;
using DeployFlow.Domain.Entities;
using Microsoft.Extensions.Options;

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
    private readonly FeatureFlagsOptions _featureFlags;

    public CreateProjectCommandHandler(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IMapper mapper,
        IOptions<FeatureFlagsOptions> featureFlags)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
        _featureFlags = featureFlags.Value;
    }

    public async Task<Result<ProjectDto>> Handle(CreateProjectCommand request, CancellationToken ct)
    {
        var existing = await _uow.Projects.GetByNameAsync(request.Name, _currentUser.TenantId, ct);
        if (existing is not null)
            return Result<ProjectDto>.Failure("A project with this name already exists.");

        var (buildCommand, installCommand) = ProjectCommandSanitizer.NormalizeDotnetCommands(
            request.Framework,
            request.BuildCommand,
            request.InstallCommand);

        var project = Domain.Entities.Project.Create(
            tenantId: _currentUser.TenantId,
            name: request.Name,
            description: request.Description,
            repositoryUrl: request.RepositoryUrl,
            repositoryBranch: request.Branch ?? "main",
            buildCommand: buildCommand,
            startCommand: request.StartCommand,
            installCommand: installCommand,
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

        if (_featureFlags.AutoPipelineEnabled)
        {
            var existingPipelines = await _uow.Pipelines.GetByProjectAsync(project.Id, ct);
            if (existingPipelines.Count == 0)
            {
                var pipeline = BuildDefaultPipeline(project, _currentUser.TenantId);
                await _uow.Pipelines.AddAsync(pipeline, ct);
                await _uow.SaveChangesAsync(ct);
            }
        }

        return Result<ProjectDto>.Success(_mapper.Map<ProjectDto>(project));
    }

    private static Pipeline BuildDefaultPipeline(Domain.Entities.Project project, Guid tenantId)
    {
        var pipeline = new Pipeline
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = $"{project.Name} Default Pipeline",
            Description = "Auto-created pipeline",
            Trigger = PipelineTriggerType.Push,
            IsEnabled = true
        };

        var stages = new[]
        {
            new PipelineStage { Name = "Setup", Order = 1, Status = PipelineStageStatus.Pending },
            new PipelineStage { Name = "Build", Order = 2, Status = PipelineStageStatus.Pending },
            new PipelineStage { Name = "Test", Order = 3, Status = PipelineStageStatus.Pending },
            new PipelineStage { Name = "Deploy", Order = 4, Status = PipelineStageStatus.Pending },
            new PipelineStage { Name = "Verify", Order = 5, Status = PipelineStageStatus.Pending },
        };

        foreach (var stage in stages)
            pipeline.Stages.Add(stage);

        var framework = (project.Framework ?? string.Empty).ToLowerInvariant();
        var isDotnet = framework.Contains("dotnet") || framework.Contains("aspnet") || framework.Contains("c#") || framework.Contains("csharp");
        var isNode = framework.Contains("node") || framework.Contains("next") || framework.Contains("react") || framework.Contains("vue") || framework.Contains("angular");
        var isJava = framework.Contains("java") || framework.Contains("spring") || framework.Contains("maven") || framework.Contains("gradle");
        var isPhp = framework.Contains("php") || framework.Contains("laravel") || framework.Contains("symfony");

        var installCommand = isDotnet ? "dotnet restore"
            : isNode ? "npm ci --prefer-offline || npm install"
            : isJava ? "mvn dependency:resolve || gradle dependencies"
            : isPhp ? "composer install --no-dev --prefer-dist"
            : (project.InstallCommand ?? "echo 'No install step configured'");

        var buildCommand = isDotnet ? "dotnet publish -c Release -o out"
            : isNode ? "npm run build"
            : isJava ? "mvn package -DskipTests || gradle build"
            : isPhp ? "php -v"
            : (project.BuildCommand ?? "echo 'No build step configured'");

        var testCommand = isDotnet ? "dotnet test"
            : isNode ? "npm test -- --passWithNoTests --watchAll=false"
            : isJava ? "mvn test || gradle test"
            : isPhp ? "php -v"
            : "echo 'No test step configured'";

        var port = project.Port ?? (isPhp ? 8080 : 3000);
        var startCommand = project.StartCommand
            ?? (isDotnet ? "dotnet out/*.dll"
            : isJava ? "java -jar target/*.jar"
            : isPhp ? "php -S 0.0.0.0:8080 -t public"
            : "npm start");

        stages[0].Steps.Add(new PipelineStep
        {
            Name = "Install dependencies",
            Type = PipelineStepType.Command,
            Status = PipelineStageStatus.Pending,
            Command = installCommand,
            Timeout = 600
        });

        stages[1].Steps.Add(new PipelineStep
        {
            Name = "Build application",
            Type = PipelineStepType.Command,
            Status = PipelineStageStatus.Pending,
            Command = buildCommand,
            Timeout = 900
        });

        stages[2].Steps.Add(new PipelineStep
        {
            Name = "Run tests",
            Type = PipelineStepType.Test,
            Status = PipelineStageStatus.Pending,
            Command = testCommand,
            Timeout = 600
        });

        stages[3].Steps.Add(new PipelineStep
        {
            Name = "Deploy application",
            Type = PipelineStepType.Deploy,
            Status = PipelineStageStatus.Pending,
            Command = startCommand,
            Timeout = 900
        });

        stages[4].Steps.Add(new PipelineStep
        {
            Name = "Health check",
            Type = PipelineStepType.Command,
            Status = PipelineStageStatus.Pending,
            Command = $"curl -sf http://localhost:{port}/health || curl -sf http://localhost:{port}/",
            Timeout = 120
        });

        return pipeline;
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

        var effectiveFramework = request.Framework ?? project.Framework;
        var (buildCommand, installCommand) = ProjectCommandSanitizer.NormalizeDotnetCommands(
            effectiveFramework,
            request.BuildCommand,
            request.InstallCommand);

        project.Update(
            name: request.Name,
            description: request.Description,
            repositoryUrl: request.RepositoryUrl,
            repositoryBranch: request.Branch,
            buildCommand: buildCommand,
            startCommand: request.StartCommand,
            installCommand: installCommand,
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

internal static class ProjectCommandSanitizer
{
    internal static (string? BuildCommand, string? InstallCommand) NormalizeDotnetCommands(
        string? framework,
        string? buildCommand,
        string? installCommand)
    {
        if (!IsDotnetFramework(framework))
            return (buildCommand, installCommand);

        // For dockerized .NET deploys, host-side dotnet build/install commands are unsafe.
        // BuildService already performs build/publish inside Dockerfile generation.
        return (null, null);
    }

    internal static bool IsDotnetFramework(string? framework)
    {
        if (string.IsNullOrWhiteSpace(framework)) return false;

        return framework.Contains("dotnet", StringComparison.OrdinalIgnoreCase)
            || framework.Contains("aspnet", StringComparison.OrdinalIgnoreCase)
            || framework.Contains("asp.net", StringComparison.OrdinalIgnoreCase)
            || framework.Contains("blazor", StringComparison.OrdinalIgnoreCase)
            || framework.Contains("csharp", StringComparison.OrdinalIgnoreCase)
            || framework.Contains("c#", StringComparison.OrdinalIgnoreCase);
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

        // Copy environment variables from source project to the clone
        var sourceEnvVars = await _uow.EnvVariables.GetByProjectAsync(src.Id, ct);
        foreach (var ev in sourceEnvVars)
        {
            var clonedVar = new Domain.Entities.EnvVariable
            {
                TenantId  = _currentUser.TenantId,
                ProjectId = clone.Id,
                Key       = ev.Key,
                Value     = ev.Value,
                Type      = ev.Type,
                IsShared  = ev.IsShared,
            };
            await _uow.EnvVariables.AddAsync(clonedVar, ct);
        }

        if (sourceEnvVars.Count > 0)
            await _uow.SaveChangesAsync(ct);

        return Result<ProjectDto>.Success(_mapper.Map<ProjectDto>(clone));
    }
}
