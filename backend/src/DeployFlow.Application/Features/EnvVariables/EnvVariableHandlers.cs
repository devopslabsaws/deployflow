using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;

namespace DeployFlow.Application.Features.EnvVariables;

// ─── Queries ──────────────────────────────────────────────────────────────────

public record GetEnvVariablesQuery(
    Guid? ProjectId = null,
    Guid? ServiceId = null
) : IRequest<Result<List<EnvVariableDto>>>;

public class GetEnvVariablesQueryHandler : IRequestHandler<GetEnvVariablesQuery, Result<List<EnvVariableDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetEnvVariablesQueryHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<List<EnvVariableDto>>> Handle(GetEnvVariablesQuery request, CancellationToken ct)
    {
        IReadOnlyList<EnvVariable> items;
        if (request.ProjectId.HasValue)
            items = await _uow.EnvVariables.GetByProjectAsync(request.ProjectId.Value, ct);
        else
            items = await _uow.EnvVariables.GetByTenantAsync(_currentUser.TenantId, ct);

        var dtos = items
            .Where(e => e.TenantId == _currentUser.TenantId)
            .Select(e => new EnvVariableDto(
                e.Id, e.Key,
                e.Type == EnvVarType.Secret ? null : e.Value, // mask secrets
                e.Type == EnvVarType.Secret,
                request.ProjectId.HasValue ? "project" : "shared",
                e.CreatedAt))
            .ToList();

        return Result<List<EnvVariableDto>>.Success(dtos);
    }
}

// ─── Upsert Env Variable ──────────────────────────────────────────────────────

public record UpsertEnvVariableCommand(
    string Key,
    string Value,
    bool IsSecret,
    string Environment,
    Guid? ProjectId,
    Guid? ServiceId
) : IRequest<Result<EnvVariableDto>>;

public class UpsertEnvVariableCommandValidator : AbstractValidator<UpsertEnvVariableCommand>
{
    public UpsertEnvVariableCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty()
            .Matches(@"^[A-Z_][A-Z0-9_]*$").WithMessage("Key must be UPPER_SNAKE_CASE.");
        RuleFor(x => x.Value).NotNull();
    }
}

public class UpsertEnvVariableCommandHandler : IRequestHandler<UpsertEnvVariableCommand, Result<EnvVariableDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IEncryptionService _encryption;

    public UpsertEnvVariableCommandHandler(IUnitOfWork uow, ICurrentUser cu, IEncryptionService enc)
    { _uow = uow; _currentUser = cu; _encryption = enc; }

    public async Task<Result<EnvVariableDto>> Handle(UpsertEnvVariableCommand request, CancellationToken ct)
    {
        // Look for existing
        var allVars = await _uow.EnvVariables.GetByTenantAsync(_currentUser.TenantId, ct);
        var existing = allVars.FirstOrDefault(e =>
            e.Key == request.Key &&
            e.ProjectId == request.ProjectId &&
            e.ServiceId == request.ServiceId);

        var storedValue = request.IsSecret ? _encryption.Encrypt(request.Value) : request.Value;

        if (existing is null)
        {
            var env = new EnvVariable
            {
                TenantId = _currentUser.TenantId,
                Key = request.Key,
                Value = storedValue,
                Type = request.IsSecret ? EnvVarType.Secret : EnvVarType.PlainText,
                ProjectId = request.ProjectId,
                ServiceId = request.ServiceId
            };
            await _uow.EnvVariables.AddAsync(env, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<EnvVariableDto>.Success(new EnvVariableDto(
                env.Id, env.Key, request.IsSecret ? null : request.Value,
                request.IsSecret, request.Environment, env.CreatedAt));
        }
        else
        {
            existing.Value = storedValue;
            existing.Type = request.IsSecret ? EnvVarType.Secret : EnvVarType.PlainText;
            await _uow.SaveChangesAsync(ct);
            return Result<EnvVariableDto>.Success(new EnvVariableDto(
                existing.Id, existing.Key, request.IsSecret ? null : request.Value,
                request.IsSecret, request.Environment, existing.CreatedAt));
        }
    }
}

// ─── Delete Env Variable ──────────────────────────────────────────────────────

public record DeleteEnvVariableCommand(Guid Id) : IRequest<Result>;

public class DeleteEnvVariableCommandHandler : IRequestHandler<DeleteEnvVariableCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteEnvVariableCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(DeleteEnvVariableCommand request, CancellationToken ct)
    {
        var env = await _uow.EnvVariables.GetByIdAsync(request.Id, ct);
        if (env is null || env.TenantId != _currentUser.TenantId)
            return Result.Failure("Environment variable not found.", 404);

        env.SoftDelete(_currentUser.UserId);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
