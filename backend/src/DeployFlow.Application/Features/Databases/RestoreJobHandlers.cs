using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using FluentValidation;
using MediatR;
using AutoMapper;

namespace DeployFlow.Application.Features.Databases;

// ─── Queries ─────────────────────────────────────────────────────────────────

public record GetRestoreJobsQuery(Guid DatabaseId) : IRequest<Result<IReadOnlyList<RestoreJobDto>>>;

public class GetRestoreJobsQueryHandler : IRequestHandler<GetRestoreJobsQuery, Result<IReadOnlyList<RestoreJobDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetRestoreJobsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<RestoreJobDto>>> Handle(GetRestoreJobsQuery request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.DatabaseId, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<IReadOnlyList<RestoreJobDto>>.Failure("Database not found.", 404);

        var jobs = await _uow.RestoreJobs.GetByTenantAsync(_currentUser.TenantId, ct);
        var dbJobs = jobs.Where(j => j.DatabaseInstanceId == request.DatabaseId)
            .OrderByDescending(j => j.CreatedAt)
            .ToList();

        var dtos = dbJobs.Select(j => _mapper.Map<RestoreJobDto>(j)).ToList();
        return Result<IReadOnlyList<RestoreJobDto>>.Success(dtos);
    }
}

public record GetRestoreJobQuery(Guid DatabaseId, Guid JobId) : IRequest<Result<RestoreJobDto>>;

public class GetRestoreJobQueryHandler : IRequestHandler<GetRestoreJobQuery, Result<RestoreJobDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetRestoreJobQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<RestoreJobDto>> Handle(GetRestoreJobQuery request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.DatabaseId, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<RestoreJobDto>.Failure("Database not found.", 404);

        var job = await _uow.RestoreJobs.GetByIdAsync(request.JobId, ct);
        if (job is null || job.TenantId != _currentUser.TenantId || job.DatabaseInstanceId != request.DatabaseId)
            return Result<RestoreJobDto>.Failure("Restore job not found.", 404);

        return Result<RestoreJobDto>.Success(_mapper.Map<RestoreJobDto>(job));
    }
}

// ─── Create Restore Job ───────────────────────────────────────────────────────

public record CreateRestoreJobCommand(
    Guid DatabaseId,
    Guid BackupId,
    string TargetDatabaseName
) : IRequest<Result<RestoreJobDto>>;

public class CreateRestoreJobCommandValidator : AbstractValidator<CreateRestoreJobCommand>
{
    public CreateRestoreJobCommandValidator()
    {
        RuleFor(x => x.DatabaseId).NotEmpty();
        RuleFor(x => x.BackupId).NotEmpty();
        RuleFor(x => x.TargetDatabaseName).NotEmpty().MaximumLength(200)
            .Matches(@"^[a-zA-Z0-9_\-]+$").WithMessage("Invalid target database name characters.");
    }
}

public class CreateRestoreJobCommandHandler : IRequestHandler<CreateRestoreJobCommand, Result<RestoreJobDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public CreateRestoreJobCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<RestoreJobDto>> Handle(CreateRestoreJobCommand request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.DatabaseId, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<RestoreJobDto>.Failure("Database not found.", 404);

        var backup = await _uow.DatabaseBackups.GetByIdAsync(request.BackupId, ct);
        if (backup is null || backup.DatabaseInstanceId != request.DatabaseId)
            return Result<RestoreJobDto>.Failure("Backup not found.", 404);

        if (backup.Status != "completed")
            return Result<RestoreJobDto>.Failure("Can only restore from completed backups.", 400);

        if (db.Status == DatabaseInstanceStatus.Restoring)
            return Result<RestoreJobDto>.Failure("Database is already restoring.", 409);

        // Create restore job
        var job = new RestoreJob
        {
            TenantId = _currentUser.TenantId,
            DatabaseInstanceId = request.DatabaseId,
            BackupId = request.BackupId,
            TargetDatabaseName = request.TargetDatabaseName,
            Status = RestoreJobStatus.Pending,
            ProgressPercent = 0
        };

        // Update database status
        db.Status = DatabaseInstanceStatus.Restoring;

        await _uow.RestoreJobs.AddAsync(job, ct);
        await _uow.Databases.UpdateAsync(db, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<RestoreJobDto>.Success(_mapper.Map<RestoreJobDto>(job));
    }
}

// ─── Update Restore Job Status (internal, for background jobs) ────────────────

public record UpdateRestoreJobStatusCommand(
    Guid JobId,
    RestoreJobStatus Status,
    int? ProgressPercent,
    string? ErrorMessage
) : IRequest<Result<RestoreJobDto>>;

public class UpdateRestoreJobStatusCommandHandler : IRequestHandler<UpdateRestoreJobStatusCommand, Result<RestoreJobDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateRestoreJobStatusCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<RestoreJobDto>> Handle(UpdateRestoreJobStatusCommand request, CancellationToken ct)
    {
        var job = await _uow.RestoreJobs.GetByIdAsync(request.JobId, ct);
        if (job is null || job.TenantId != _currentUser.TenantId)
            return Result<RestoreJobDto>.Failure("Restore job not found.", 404);

        job.Status = request.Status;
        if (request.ProgressPercent.HasValue)
            job.ProgressPercent = request.ProgressPercent.Value;
        if (!string.IsNullOrWhiteSpace(request.ErrorMessage))
            job.ErrorMessage = request.ErrorMessage;

        if (request.Status == RestoreJobStatus.Running && !job.StartedAt.HasValue)
            job.StartedAt = DateTime.UtcNow;

        if (request.Status == RestoreJobStatus.Success || request.Status == RestoreJobStatus.Failed)
        {
            job.CompletedAt = DateTime.UtcNow;
            job.ProgressPercent = 100;

            // Update database status back to Running
            var db = await _uow.Databases.GetByIdAsync(job.DatabaseInstanceId, ct);
            if (db != null)
            {
                db.Status = request.Status == RestoreJobStatus.Success 
                    ? DatabaseInstanceStatus.Running 
                    : DatabaseInstanceStatus.Error;
                await _uow.Databases.UpdateAsync(db, ct);
            }
        }

        await _uow.RestoreJobs.UpdateAsync(job, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<RestoreJobDto>.Success(_mapper.Map<RestoreJobDto>(job));
    }
}
