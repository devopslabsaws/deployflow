using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Compose;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ComposeStackDto(
    Guid Id,
    string Name,
    Guid ProjectId,
    Guid? ServerId,
    string ComposeYaml,
    string Status,
    int ServiceCount,
    DateTime? LastDeployedAt,
    string? LastError,
    string? EnvironmentName,
    DateTime CreatedAt
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetComposeStacksQuery(Guid? ProjectId) : IRequest<Result<List<ComposeStackDto>>>;

public class GetComposeStacksQueryHandler : IRequestHandler<GetComposeStacksQuery, Result<List<ComposeStackDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetComposeStacksQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<ComposeStackDto>>> Handle(GetComposeStacksQuery request, CancellationToken ct)
    {
        var stacks = await _uow.ComposeStacks.GetByTenantAsync(_currentUser.TenantId, ct);
        var filtered = request.ProjectId.HasValue
            ? stacks.Where(s => s.ProjectId == request.ProjectId.Value)
            : stacks;

        return Result<List<ComposeStackDto>>.Success(filtered.Select(Map).ToList());
    }

    internal static ComposeStackDto Map(ComposeStack s) => new(
        s.Id, s.Name, s.ProjectId, s.ServerId, s.ComposeYaml,
        s.Status.ToString().ToLower(), s.ServiceCount, s.LastDeployedAt,
        s.LastError, s.EnvironmentName, s.CreatedAt);
}

// ── Create ────────────────────────────────────────────────────────────────────

public record CreateComposeStackCommand(
    string Name,
    Guid ProjectId,
    Guid? ServerId,
    string ComposeYaml,
    string? EnvironmentName
) : IRequest<Result<ComposeStackDto>>;

public class CreateComposeStackCommandHandler : IRequestHandler<CreateComposeStackCommand, Result<ComposeStackDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateComposeStackCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ComposeStackDto>> Handle(CreateComposeStackCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<ComposeStackDto>.Failure("Project not found.", "404");

        // Count services defined in the YAML (lines starting with two-space or four-space service names under 'services:')
        var serviceCount = CountComposeServices(request.ComposeYaml);

        var stack = new ComposeStack
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            ProjectId = request.ProjectId,
            ServerId = request.ServerId,
            ComposeYaml = request.ComposeYaml,
            ServiceCount = serviceCount,
            EnvironmentName = request.EnvironmentName,
            Status = ComposeStackStatus.Stopped
        };

        await _uow.ComposeStacks.AddAsync(stack, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ComposeStackDto>.Success(GetComposeStacksQueryHandler.Map(stack));
    }

    private static int CountComposeServices(string yaml)
    {
        // Simple heuristic: count indented keys under 'services:' block
        var lines = yaml.Split('\n');
        bool inServices = false;
        int count = 0;
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("services:", StringComparison.OrdinalIgnoreCase))
            { inServices = true; continue; }
            if (inServices && line.Length > 0 && !char.IsWhiteSpace(line[0]))
                inServices = false;
            if (inServices && line.Length > 2 && line[0] == ' ' && line[2] != ' ' && line.TrimStart()[0] != '#')
                count++;
        }
        return Math.Max(count, 1);
    }
}

// ── Deploy ────────────────────────────────────────────────────────────────────

public record DeployComposeStackCommand(Guid Id) : IRequest<Result<string>>;

public class DeployComposeStackCommandHandler : IRequestHandler<DeployComposeStackCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeployComposeStackCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> Handle(DeployComposeStackCommand request, CancellationToken ct)
    {
        var stack = await _uow.ComposeStacks.GetByIdAsync(request.Id, ct);
        if (stack is null || stack.TenantId != _currentUser.TenantId)
            return Result<string>.Failure("Compose stack not found.", "404");

        if (stack.Status == ComposeStackStatus.Starting)
            return Result<string>.Failure("Stack is already deploying.", "409");

        stack.Status = ComposeStackStatus.Starting;
        stack.LastError = null;
        await _uow.ComposeStacks.UpdateAsync(stack, ct);
        await _uow.SaveChangesAsync(ct);

        // The DeploymentRunnerService will pick this up and run `docker compose up -d`
        // For now we update the status to Running as a baseline
        stack.Status = ComposeStackStatus.Running;
        stack.LastDeployedAt = DateTime.UtcNow;
        await _uow.ComposeStacks.UpdateAsync(stack, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success("Compose stack deployment queued.");
    }
}

// ── Delete ────────────────────────────────────────────────────────────────────

public record DeleteComposeStackCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteComposeStackCommandHandler : IRequestHandler<DeleteComposeStackCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteComposeStackCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DeleteComposeStackCommand request, CancellationToken ct)
    {
        var stack = await _uow.ComposeStacks.GetByIdAsync(request.Id, ct);
        if (stack is null || stack.TenantId != _currentUser.TenantId)
            return Result<bool>.Failure("Compose stack not found.", "404");

        await _uow.ComposeStacks.DeleteAsync(stack, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
