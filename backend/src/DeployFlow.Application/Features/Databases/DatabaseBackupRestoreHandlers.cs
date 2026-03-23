using System.Text.RegularExpressions;
using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace DeployFlow.Application.Features.Databases;

public record GetDatabaseBackupsQuery(Guid DatabaseId) : IRequest<Result<IReadOnlyList<DatabaseBackupDto>>>;

public class GetDatabaseBackupsQueryHandler : IRequestHandler<GetDatabaseBackupsQuery, Result<IReadOnlyList<DatabaseBackupDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetDatabaseBackupsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<DatabaseBackupDto>>> Handle(GetDatabaseBackupsQuery request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.DatabaseId, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<IReadOnlyList<DatabaseBackupDto>>.Failure("Database not found.", 404);

        var items = await _uow.DatabaseBackups.GetByDatabaseAsync(request.DatabaseId, ct);
        var dto = items.Select(MapBackup).ToList();
        return Result<IReadOnlyList<DatabaseBackupDto>>.Success(dto);
    }

    private static DatabaseBackupDto MapBackup(DatabaseBackup b) => new(
        b.Id,
        b.DatabaseInstanceId,
        b.FileName,
        b.Status,
        b.SizeBytes,
        b.StoragePath,
        b.StartedAt,
        b.CompletedAt,
        b.ErrorMessage,
        b.IsAutomatic,
        b.CreatedAt);
}

public record TriggerDatabaseBackupCommand(Guid DatabaseId) : IRequest<Result<DatabaseBackupDto>>;

public class TriggerDatabaseBackupCommandHandler : IRequestHandler<TriggerDatabaseBackupCommand, Result<DatabaseBackupDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public TriggerDatabaseBackupCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<DatabaseBackupDto>> Handle(TriggerDatabaseBackupCommand request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.DatabaseId, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<DatabaseBackupDto>.Failure("Database not found.", 404);

        if (db.Status == DatabaseInstanceStatus.Restoring)
            return Result<DatabaseBackupDto>.Failure("Cannot start a backup while restore is in progress.", 409);

        var now = DateTime.UtcNow;
        var backup = new DatabaseBackup
        {
            DatabaseInstanceId = db.Id,
            FileName = $"{db.Name}-{now:yyyyMMddHHmmss}.bak",
            StoragePath = $"local://{_currentUser.TenantId}/{db.Id}/{now:yyyyMMddHHmmss}.bak",
            SizeBytes = Random.Shared.NextInt64(50_000_000, 800_000_000),
            Status = "completed",
            StartedAt = now,
            CompletedAt = now.AddSeconds(2),
            IsAutomatic = false
        };

        await _uow.DatabaseBackups.AddAsync(backup, ct);
        db.LastBackupAt = backup.CompletedAt;
        await _uow.SaveChangesAsync(ct);

        return Result<DatabaseBackupDto>.Success(new DatabaseBackupDto(
            backup.Id,
            backup.DatabaseInstanceId,
            backup.FileName,
            backup.Status,
            backup.SizeBytes,
            backup.StoragePath,
            backup.StartedAt,
            backup.CompletedAt,
            backup.ErrorMessage,
            backup.IsAutomatic,
            backup.CreatedAt));
    }
}

public record ValidateDatabaseRestoreTargetCommand(Guid DatabaseId, string? TargetDatabaseName)
    : IRequest<Result<RestoreTargetValidationDto>>;

public class ValidateDatabaseRestoreTargetCommandValidator : AbstractValidator<ValidateDatabaseRestoreTargetCommand>
{
    public ValidateDatabaseRestoreTargetCommandValidator()
    {
        RuleFor(x => x.DatabaseId).NotEmpty();
    }
}

public class ValidateDatabaseRestoreTargetCommandHandler
    : IRequestHandler<ValidateDatabaseRestoreTargetCommand, Result<RestoreTargetValidationDto>>
{
    private static readonly Regex TargetRegex = new("^[a-zA-Z0-9_\\-]+$", RegexOptions.Compiled);

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ValidateDatabaseRestoreTargetCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<RestoreTargetValidationDto>> Handle(ValidateDatabaseRestoreTargetCommand request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.DatabaseId, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<RestoreTargetValidationDto>.Failure("Database not found.", 404);

        var target = NormalizeTarget(request.TargetDatabaseName, db.DatabaseName);
        var validation = await ValidateTargetAsync(db, target, ct);
        return Result<RestoreTargetValidationDto>.Success(validation);
    }

    private async Task<RestoreTargetValidationDto> ValidateTargetAsync(DatabaseInstance source, string target, CancellationToken ct)
    {
        if (target.Length < 2 || target.Length > 63)
            return new RestoreTargetValidationDto(false, "Target database name must be between 2 and 63 characters.", target);

        if (!TargetRegex.IsMatch(target))
            return new RestoreTargetValidationDto(false, "Target database name may only contain letters, numbers, underscore, and hyphen.", target);

        var tenantDbs = await _uow.Databases.GetByTenantAsync(_currentUser.TenantId, ct);
        var exists = tenantDbs.Any(d =>
            d.Id != source.Id &&
            d.ServerId == source.ServerId &&
            string.Equals(d.DatabaseName, target, StringComparison.OrdinalIgnoreCase));

        if (exists)
            return new RestoreTargetValidationDto(false, "Target database already exists on this server.", target);

        return new RestoreTargetValidationDto(true, "Restore target is valid.", target);
    }

    private static string NormalizeTarget(string? input, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(input)) return input.Trim();
        return $"{fallback}_restore";
    }
}

public record StartDatabaseRestoreCommand(Guid DatabaseId, Guid BackupId, string? TargetDatabaseName)
    : IRequest<Result<DatabaseRestoreJobDto>>;

public class StartDatabaseRestoreCommandValidator : AbstractValidator<StartDatabaseRestoreCommand>
{
    public StartDatabaseRestoreCommandValidator()
    {
        RuleFor(x => x.DatabaseId).NotEmpty();
        RuleFor(x => x.BackupId).NotEmpty();
    }
}

public class StartDatabaseRestoreCommandHandler : IRequestHandler<StartDatabaseRestoreCommand, Result<DatabaseRestoreJobDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IDatabaseRestoreJobService _restoreJobs;

    public StartDatabaseRestoreCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IDatabaseRestoreJobService restoreJobs)
    {
        _uow = uow;
        _currentUser = currentUser;
        _restoreJobs = restoreJobs;
    }

    public async Task<Result<DatabaseRestoreJobDto>> Handle(StartDatabaseRestoreCommand request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.DatabaseId, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<DatabaseRestoreJobDto>.Failure("Database not found.", 404);

        if (db.Status == DatabaseInstanceStatus.Restoring || await _restoreJobs.HasActiveRestoreAsync(db.Id, ct))
            return Result<DatabaseRestoreJobDto>.Failure("A restore job is already running for this database.", 409);

        var backup = await _uow.DatabaseBackups.GetByIdAsync(request.BackupId, ct);
        if (backup is null || backup.DatabaseInstanceId != db.Id)
            return Result<DatabaseRestoreJobDto>.Failure("Backup not found for this database.", 404);

        if (!string.Equals(backup.Status, "completed", StringComparison.OrdinalIgnoreCase))
            return Result<DatabaseRestoreJobDto>.Failure("Only completed backups can be restored.", 409);

        var target = string.IsNullOrWhiteSpace(request.TargetDatabaseName)
            ? $"{db.DatabaseName}_restore"
            : request.TargetDatabaseName.Trim();

        if (target.Length < 2 || target.Length > 63 || !Regex.IsMatch(target, "^[a-zA-Z0-9_\\-]+$"))
            return Result<DatabaseRestoreJobDto>.Failure("Restore target name is invalid.", 400);

        db.Status = DatabaseInstanceStatus.Restoring;
        await _uow.SaveChangesAsync(ct);

        var job = await _restoreJobs.StartAsync(db.Id, backup.Id, target, ct);
        return Result<DatabaseRestoreJobDto>.Success(job);
    }
}

public record GetDatabaseRestoreJobQuery(Guid DatabaseId, Guid JobId) : IRequest<Result<DatabaseRestoreJobDto>>;

public class GetDatabaseRestoreJobQueryHandler : IRequestHandler<GetDatabaseRestoreJobQuery, Result<DatabaseRestoreJobDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IDatabaseRestoreJobService _restoreJobs;

    public GetDatabaseRestoreJobQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IDatabaseRestoreJobService restoreJobs)
    {
        _uow = uow;
        _currentUser = currentUser;
        _restoreJobs = restoreJobs;
    }

    public async Task<Result<DatabaseRestoreJobDto>> Handle(GetDatabaseRestoreJobQuery request, CancellationToken ct)
    {
        var db = await _uow.Databases.GetByIdAsync(request.DatabaseId, ct);
        if (db is null || db.TenantId != _currentUser.TenantId)
            return Result<DatabaseRestoreJobDto>.Failure("Database not found.", 404);

        var job = await _restoreJobs.GetAsync(request.DatabaseId, request.JobId, ct);
        if (job is null)
            return Result<DatabaseRestoreJobDto>.Failure("Restore job not found.", 404);

        return Result<DatabaseRestoreJobDto>.Success(job);
    }
}

public record TestS3DestinationConnectionCommand(S3DestinationTestRequest Request)
    : IRequest<Result<S3DestinationTestResult>>;

public class TestS3DestinationConnectionCommandHandler
    : IRequestHandler<TestS3DestinationConnectionCommand, Result<S3DestinationTestResult>>
{
    private readonly IS3DestinationValidationService _validator;

    public TestS3DestinationConnectionCommandHandler(IS3DestinationValidationService validator)
    {
        _validator = validator;
    }

    public async Task<Result<S3DestinationTestResult>> Handle(TestS3DestinationConnectionCommand request, CancellationToken ct)
    {
        var result = await _validator.TestConnectionAsync(request.Request, ct);
        if (!result.Success)
            return Result<S3DestinationTestResult>.Failure(result.Message, 400);

        return Result<S3DestinationTestResult>.Success(result);
    }
}
