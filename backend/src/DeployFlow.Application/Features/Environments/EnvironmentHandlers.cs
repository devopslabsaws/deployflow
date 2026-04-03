using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Environments;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record EnvironmentDto(
    string Id,
    string Name,
    string Slug,
    bool IsDefault,
    bool IsProduction,
    int Order
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetEnvironmentsQuery : IRequest<Result<List<EnvironmentDto>>>;

public class GetEnvironmentsQueryHandler : IRequestHandler<GetEnvironmentsQuery, Result<List<EnvironmentDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetEnvironmentsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<EnvironmentDto>>> Handle(GetEnvironmentsQuery request, CancellationToken ct)
    {
        var envs = await _uow.Environments.GetByTenantAsync(_currentUser.TenantId, ct);
        var dtos = envs.OrderBy(e => e.Order).Select(Map).ToList();

        // Seed defaults if none exist
        if (!dtos.Any())
        {
            var defaults = new[]
            {
                new EnvironmentDto("dev", "Development", "dev", false, false, 0),
                new EnvironmentDto("qa", "QA", "qa", false, false, 1),
                new EnvironmentDto("staging", "Staging", "staging", false, false, 2),
                new EnvironmentDto("production", "Production", "production", true, true, 3),
            };
            return Result<List<EnvironmentDto>>.Success(defaults.ToList());
        }

        return Result<List<EnvironmentDto>>.Success(dtos);
    }

    private static EnvironmentDto Map(ProjectEnvironment e) => new(
        e.Id.ToString(), e.Name, e.Slug, e.IsDefault, e.IsProduction, e.Order);
}

// ── Commands ──────────────────────────────────────────────────────────────────

public record CreateEnvironmentCommand(string Name, string Slug, bool IsProduction, int Order) : IRequest<Result<EnvironmentDto>>;

public class CreateEnvironmentCommandHandler : IRequestHandler<CreateEnvironmentCommand, Result<EnvironmentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateEnvironmentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<EnvironmentDto>> Handle(CreateEnvironmentCommand request, CancellationToken ct)
    {
        var env = new ProjectEnvironment
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            Slug = request.Slug.ToLowerInvariant(),
            IsProduction = request.IsProduction,
            Order = request.Order,
        };
        await _uow.Environments.AddAsync(env, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<EnvironmentDto>.Success(Map(env));
    }

    private static EnvironmentDto Map(ProjectEnvironment e) => new(
        e.Id.ToString(), e.Name, e.Slug, e.IsDefault, e.IsProduction, e.Order);
}

public record DeleteEnvironmentCommand(Guid Id) : IRequest<Result>;

public class DeleteEnvironmentCommandHandler : IRequestHandler<DeleteEnvironmentCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteEnvironmentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteEnvironmentCommand request, CancellationToken ct)
    {
        var env = await _uow.Environments.GetByIdAsync(request.Id, ct);
        if (env is null || env.TenantId != _currentUser.TenantId)
            return Result.Failure("Environment not found.", "404");

        if (env.IsProduction)
            return Result.Failure("Cannot delete the production environment.", "400");

        await _uow.Environments.DeleteAsync(env, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
