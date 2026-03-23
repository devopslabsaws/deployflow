using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using FluentValidation;
using MediatR;
using AutoMapper;

namespace DeployFlow.Application.Features.Databases;

// ─── Queries ─────────────────────────────────────────────────────────────────

public record GetBackupPolicyQuery(Guid DatabaseId) : IRequest<Result<BackupPolicyDto>>;

public class GetBackupPolicyQueryHandler : IRequestHandler<GetBackupPolicyQuery, Result<BackupPolicyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetBackupPolicyQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<BackupPolicyDto>> Handle(GetBackupPolicyQuery request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.DatabaseId, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<BackupPolicyDto>.Failure("Database not found.", 404);

        // Find or create default policy
        var policies = await _uow.BackupPolicies.GetByTenantAsync(_currentUser.TenantId, ct);
        var policy = policies.FirstOrDefault(p => p.DatabaseInstanceId == request.DatabaseId);
        
        if (policy is null)
        {
            // Create default policy
            policy = new BackupPolicy
            {
                TenantId = _currentUser.TenantId,
                DatabaseInstanceId = request.DatabaseId,
                IsEnabled = db.BackupEnabled,
                CronExpression = db.BackupSchedule ?? "0 2 * * *",
                RetentionDays = db.BackupRetentionDays,
                StorageLocation = "local"
            };
            await _uow.BackupPolicies.AddAsync(policy, ct);
            await _uow.SaveChangesAsync(ct);
        }

        return Result<BackupPolicyDto>.Success(_mapper.Map<BackupPolicyDto>(policy));
    }
}

public record ListBackupPoliciesQuery : IRequest<Result<IReadOnlyList<BackupPolicyDto>>>;

public class ListBackupPoliciesQueryHandler : IRequestHandler<ListBackupPoliciesQuery, Result<IReadOnlyList<BackupPolicyDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public ListBackupPoliciesQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<BackupPolicyDto>>> Handle(ListBackupPoliciesQuery request, CancellationToken ct)
    {
        var items = await _uow.BackupPolicies.GetByTenantAsync(_currentUser.TenantId, ct);
        var dtos = items.Select(p => _mapper.Map<BackupPolicyDto>(p)).ToList();
        return Result<IReadOnlyList<BackupPolicyDto>>.Success(dtos);
    }
}

// ─── Create/Update Backup Policy ──────────────────────────────────────────────

public record UpdateBackupPolicyCommand(
    Guid DatabaseId,
    bool? IsEnabled,
    string? CronExpression,
    int? RetentionDays,
    Guid? S3DestinationId,
    string? StorageLocation
) : IRequest<Result<BackupPolicyDto>>;

public class UpdateBackupPolicyCommandValidator : AbstractValidator<UpdateBackupPolicyCommand>
{
    public UpdateBackupPolicyCommandValidator()
    {
        RuleFor(x => x.DatabaseId).NotEmpty();
        RuleFor(x => x.RetentionDays).GreaterThan(0).LessThanOrEqualTo(3650).When(x => x.RetentionDays.HasValue);
    }
}

public class UpdateBackupPolicyCommandHandler : IRequestHandler<UpdateBackupPolicyCommand, Result<BackupPolicyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateBackupPolicyCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<BackupPolicyDto>> Handle(UpdateBackupPolicyCommand request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.DatabaseId, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<BackupPolicyDto>.Failure("Database not found.", 404);

        var policies = await _uow.BackupPolicies.GetByTenantAsync(_currentUser.TenantId, ct);
        var policy = policies.FirstOrDefault(p => p.DatabaseInstanceId == request.DatabaseId);

        if (policy is null)
        {
            // Create new policy with provided values
            policy = new BackupPolicy
            {
                TenantId = _currentUser.TenantId,
                DatabaseInstanceId = request.DatabaseId,
                IsEnabled = request.IsEnabled ?? db.BackupEnabled,
                CronExpression = request.CronExpression ?? (db.BackupSchedule ?? "0 2 * * *"),
                RetentionDays = request.RetentionDays ?? db.BackupRetentionDays,
                S3DestinationId = request.S3DestinationId,
                StorageLocation = request.StorageLocation ?? "local"
            };
            await _uow.BackupPolicies.AddAsync(policy, ct);
        }
        else
        {
            // Update existing policy
            if (request.IsEnabled.HasValue) policy.IsEnabled = request.IsEnabled.Value;
            if (!string.IsNullOrWhiteSpace(request.CronExpression)) policy.CronExpression = request.CronExpression;
            if (request.RetentionDays.HasValue) policy.RetentionDays = request.RetentionDays.Value;
            if (request.S3DestinationId.HasValue || request.S3DestinationId == Guid.Empty) 
                policy.S3DestinationId = request.S3DestinationId == Guid.Empty ? null : request.S3DestinationId;
            if (!string.IsNullOrWhiteSpace(request.StorageLocation)) policy.StorageLocation = request.StorageLocation;

            await _uow.BackupPolicies.UpdateAsync(policy, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<BackupPolicyDto>.Success(_mapper.Map<BackupPolicyDto>(policy));
    }
}
