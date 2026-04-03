using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;

namespace DeployFlow.Application.Features.Containers;

// ─── Queries ─────────────────────────────────────────────────────────────────

public record GetContainersQuery(string ServerId) : IRequest<Result<List<ContainerDto>>>;

public class GetContainersQueryHandler : IRequestHandler<GetContainersQuery, Result<List<ContainerDto>>>
{
    private readonly IServerRepository _serverRepository;
    private readonly IDockerService _dockerService;
    private readonly ICurrentUser _currentUser;

    public GetContainersQueryHandler(IServerRepository serverRepository, IDockerService dockerService, ICurrentUser currentUser)
    {
        _serverRepository = serverRepository;
        _dockerService = dockerService;
        _currentUser = currentUser;
    }

    public async Task<Result<List<ContainerDto>>> Handle(GetContainersQuery request, CancellationToken ct)
    {
        // Containers are fetched via docker ps exec, not persisted in DB
        // This handler is a placeholder for future enhancement
        return Result<List<ContainerDto>>.Success(new List<ContainerDto>());
    }
}

// ─── Commands: Start Container ──────────────────────────────────────────────

public record StartContainerCommand(string ServerId, string ContainerId) : IRequest<Result<string>>;

public class StartContainerCommandValidator : AbstractValidator<StartContainerCommand>
{
    public StartContainerCommandValidator()
    {
        RuleFor(x => x.ServerId).NotEmpty();
        RuleFor(x => x.ContainerId).NotEmpty().Length(1, 64);
    }
}

public class StartContainerCommandHandler : IRequestHandler<StartContainerCommand, Result<string>>
{
    private readonly IDockerService _dockerService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public StartContainerCommandHandler(IDockerService dockerService, ICurrentUser currentUser, IAuditService auditService)
    {
        _dockerService = dockerService;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<string>> Handle(StartContainerCommand request, CancellationToken ct)
    {
        try
        {
            await _dockerService.StartContainerAsync(request.ServerId, request.ContainerId, ct);

            await _auditService.LogAsync(
                _currentUser.UserId,
                _currentUser.Name,
                "StartContainer",
                "Container",
                Guid.Empty,
                request.ContainerId,
                _currentUser.TenantId,
                ct: ct
            );

            return Result<string>.Success($"Container {request.ContainerId} started successfully");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to start container: {ex.Message}", 500);
        }
    }
}

// ─── Commands: Stop Container ──────────────────────────────────────────────

public record StopContainerCommand(string ServerId, string ContainerId) : IRequest<Result<string>>;

public class StopContainerCommandValidator : AbstractValidator<StopContainerCommand>
{
    public StopContainerCommandValidator()
    {
        RuleFor(x => x.ServerId).NotEmpty();
        RuleFor(x => x.ContainerId).NotEmpty().Length(1, 64);
    }
}

public class StopContainerCommandHandler : IRequestHandler<StopContainerCommand, Result<string>>
{
    private readonly IDockerService _dockerService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public StopContainerCommandHandler(IDockerService dockerService, ICurrentUser currentUser, IAuditService auditService)
    {
        _dockerService = dockerService;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<string>> Handle(StopContainerCommand request, CancellationToken ct)
    {
        try
        {
            await _dockerService.StopContainerAsync(request.ServerId, request.ContainerId, ct);
            
            await _auditService.LogAsync(
                _currentUser.UserId,
                _currentUser.Name,
                "StopContainer",
                "Container",
                Guid.Empty,
                request.ContainerId,
                _currentUser.TenantId,
                ct: ct
            );

            return Result<string>.Success($"Container {request.ContainerId} stopped successfully");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to stop container: {ex.Message}", 500);
        }
    }
}

// ─── Commands: Restart Container ────────────────────────────────────────────

public record RestartContainerCommand(string ServerId, string ContainerId) : IRequest<Result<string>>;

public class RestartContainerCommandValidator : AbstractValidator<RestartContainerCommand>
{
    public RestartContainerCommandValidator()
    {
        RuleFor(x => x.ServerId).NotEmpty();
        RuleFor(x => x.ContainerId).NotEmpty().Length(1, 64);
    }
}

public class RestartContainerCommandHandler : IRequestHandler<RestartContainerCommand, Result<string>>
{
    private readonly IDockerService _dockerService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public RestartContainerCommandHandler(IDockerService dockerService, ICurrentUser currentUser, IAuditService auditService)
    {
        _dockerService = dockerService;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<string>> Handle(RestartContainerCommand request, CancellationToken ct)
    {
        try
        {
            await _dockerService.RestartContainerAsync(request.ServerId, request.ContainerId, ct);

            await _auditService.LogAsync(
                _currentUser.UserId,
                _currentUser.Name,
                "RestartContainer",
                "Container",
                Guid.Empty,
                request.ContainerId,
                _currentUser.TenantId,
                ct: ct
            );

            return Result<string>.Success($"Container {request.ContainerId} restarted successfully");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to restart container: {ex.Message}", 500);
        }
    }
}

// ─── Commands: Remove Container ────────────────────────────────────────────

public record RemoveContainerCommand(string ServerId, string ContainerId) : IRequest<Result<string>>;

public class RemoveContainerCommandValidator : AbstractValidator<RemoveContainerCommand>
{
    public RemoveContainerCommandValidator()
    {
        RuleFor(x => x.ServerId).NotEmpty();
        RuleFor(x => x.ContainerId).NotEmpty().Length(1, 64);
    }
}

public class RemoveContainerCommandHandler : IRequestHandler<RemoveContainerCommand, Result<string>>
{
    private readonly IDockerService _dockerService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public RemoveContainerCommandHandler(IDockerService dockerService, ICurrentUser currentUser, IAuditService auditService)
    {
        _dockerService = dockerService;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<string>> Handle(RemoveContainerCommand request, CancellationToken ct)
    {
        try
        {
            await _dockerService.RemoveContainerAsync(request.ServerId, request.ContainerId, ct);
            
            await _auditService.LogAsync(
                _currentUser.UserId,
                _currentUser.Name,
                "RemoveContainer",
                "Container",
                Guid.Empty,
                request.ContainerId,
                _currentUser.TenantId,
                ct: ct
            );

            return Result<string>.Success($"Container {request.ContainerId} removed successfully");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to remove container: {ex.Message}", 500);
        }
    }
}

// ─── Queries: Get Container Logs ────────────────────────────────────────────

public record GetContainerLogsQuery(string ServerId, string ContainerId, int? Lines = 100) : IRequest<Result<string>>;

public class GetContainerLogsQueryValidator : AbstractValidator<GetContainerLogsQuery>
{
    public GetContainerLogsQueryValidator()
    {
        RuleFor(x => x.ServerId).NotEmpty();
        RuleFor(x => x.ContainerId).NotEmpty().Length(1, 64);
        RuleFor(x => x.Lines).GreaterThan(0).LessThanOrEqualTo(10000).When(x => x.Lines.HasValue);
    }
}

public class GetContainerLogsQueryHandler : IRequestHandler<GetContainerLogsQuery, Result<string>>
{
    private readonly IDockerService _dockerService;
    private readonly ICurrentUser _currentUser;

    public GetContainerLogsQueryHandler(IDockerService dockerService, ICurrentUser currentUser)
    {
        _dockerService = dockerService;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> Handle(GetContainerLogsQuery request, CancellationToken ct)
    {
        try
        {
            // Fetch logs from Docker service (returns IAsyncEnumerable<string>)
            var logsEnumerable = _dockerService.StreamLogsAsync(request.ServerId, request.ContainerId, false, ct);
            var logs = new System.Collections.Generic.List<string>();
            
            await foreach (var logLine in logsEnumerable.ConfigureAwait(false))
            {
                logs.Add(logLine);
                if (request.Lines.HasValue && logs.Count >= request.Lines.Value)
                    break;
            }
            
            return Result<string>.Success(string.Join("\n", logs));
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to fetch container logs: {ex.Message}", 500);
        }
    }
}
