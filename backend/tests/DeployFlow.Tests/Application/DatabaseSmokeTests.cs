using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Databases;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DeployFlow.Tests.Application;

/// <summary>
/// Targeted smoke tests for:
///   A) Delete-database safe-delete guardrails (409 conflicts)
///   B) Restore-target validation handler (field rules + duplicate check)
///   C) Restore start handler (pre-conditions and job creation)
///   D) Restore job poll handler (404 and status propagation)
///   E) S3 destination test handler (delegates to IS3DestinationValidationService)
///
/// All I/O is replaced by Moq stubs — no database or HTTP connection is required.
/// </summary>
public class DatabaseSmokeTests
{
    // ─── Shared fixtures ──────────────────────────────────────────────────────

    private static readonly Guid _tenantId = Guid.NewGuid();
    private static readonly Guid _userId   = Guid.NewGuid();

    private static ICurrentUser CurrentUser()
    {
        var cu = new Mock<ICurrentUser>();
        cu.Setup(x => x.TenantId).Returns(_tenantId);
        cu.Setup(x => x.UserId).Returns(_userId);
        return cu.Object;
    }

    private static (Mock<IUnitOfWork> uow, Mock<IDatabaseRepository> dbRepo, Mock<IDatabaseBackupRepository> backupRepo)
        MakeUow()
    {
        var dbRepo     = new Mock<IDatabaseRepository>();
        var backupRepo = new Mock<IDatabaseBackupRepository>();
        var uow        = new Mock<IUnitOfWork>();

        uow.Setup(x => x.Databases).Returns(dbRepo.Object);
        uow.Setup(x => x.DatabaseBackups).Returns(backupRepo.Object);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return (uow, dbRepo, backupRepo);
    }

    /// <summary>
    /// Creates a DatabaseInstance whose TenantId matches the shared tenant.
    /// All public properties are settable directly — Id is auto-generated in BaseEntity.
    /// </summary>
    private static DatabaseInstance MakeDb(
        bool backupEnabled = false,
        DatabaseInstanceStatus status = DatabaseInstanceStatus.Running)
        => new()
        {
            TenantId          = _tenantId,
            Name              = "test-db",
            Engine            = DatabaseEngine.PostgreSQL,
            Version           = "15",
            DatabaseName      = "testdb",
            Username          = "admin",
            PasswordEncrypted = "enc",
            ServerId          = Guid.NewGuid(),
            BackupEnabled     = backupEnabled,
            Status            = status,
        };

    // ═════════════════════════════════════════════════════════════════════════
    // A. DeleteDatabaseCommandHandler — safe-delete guardrails
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Delete_DatabaseNotFound_Returns404()
    {
        var (uow, dbRepo, _) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        dbRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((DatabaseInstance?)null);

        var handler = new DeleteDatabaseCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new DeleteDatabaseCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("404");
    }

    [Fact]
    public async Task Delete_StatusIsRestoring_Returns409_WithRestoreMessage()
    {
        var (uow, dbRepo, _) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb(status: DatabaseInstanceStatus.Restoring);
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);

        var handler = new DeleteDatabaseCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new DeleteDatabaseCommand(db.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("409");
        result.Error.Should().Contain("restore is in progress");
    }

    [Fact]
    public async Task Delete_ActiveRestoreJobExists_Returns409()
    {
        var (uow, dbRepo, _) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb();
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.HasActiveRestoreAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new DeleteDatabaseCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new DeleteDatabaseCommand(db.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("409");
    }

    [Fact]
    public async Task Delete_BackupEnabled_NoCompletedBackup_Returns409()
    {
        var (uow, dbRepo, backupRepo) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb(backupEnabled: true);
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.HasActiveRestoreAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        backupRepo.Setup(x => x.GetLatestCompletedAsync(db.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((DatabaseBackup?)null);

        var handler = new DeleteDatabaseCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new DeleteDatabaseCommand(db.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("409");
        result.Error.Should().Contain("no completed backup");
    }

    [Fact]
    public async Task Delete_BackupEnabled_StaleBackup_Returns409_WithStalenessMessage()
    {
        var (uow, dbRepo, backupRepo) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb(backupEnabled: true);
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.HasActiveRestoreAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var staleBackup = new DatabaseBackup
        {
            DatabaseInstanceId = db.Id,
            FileName           = "old.bak",
            Status             = "completed",
            CompletedAt        = DateTime.UtcNow.AddDays(-10), // > 7 days old
        };
        backupRepo.Setup(x => x.GetLatestCompletedAsync(db.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(staleBackup);

        var handler = new DeleteDatabaseCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new DeleteDatabaseCommand(db.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("409");
        result.Error.Should().Contain("older than 7 days");
    }

    [Fact]
    public async Task Delete_BackupDisabled_Succeeds_WithoutCheckingBackupRepo()
    {
        var (uow, dbRepo, backupRepo) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb(backupEnabled: false);
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.HasActiveRestoreAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new DeleteDatabaseCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new DeleteDatabaseCommand(db.Id), default);

        result.IsSuccess.Should().BeTrue();
        backupRepo.Verify(
            x => x.GetLatestCompletedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "backup repo should not be queried when backups are disabled");
    }

    [Fact]
    public async Task Delete_BackupEnabled_FreshBackup_Succeeds()
    {
        var (uow, dbRepo, backupRepo) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb(backupEnabled: true);
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.HasActiveRestoreAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var freshBackup = new DatabaseBackup
        {
            DatabaseInstanceId = db.Id,
            FileName           = "recent.bak",
            Status             = "completed",
            CompletedAt        = DateTime.UtcNow.AddHours(-2),
        };
        backupRepo.Setup(x => x.GetLatestCompletedAsync(db.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(freshBackup);

        var handler = new DeleteDatabaseCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new DeleteDatabaseCommand(db.Id), default);

        result.IsSuccess.Should().BeTrue();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // B. ValidateDatabaseRestoreTargetCommandHandler
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidateRestoreTarget_DatabaseNotFound_Returns404()
    {
        var (uow, dbRepo, _) = MakeUow();
        dbRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((DatabaseInstance?)null);

        var handler = new ValidateDatabaseRestoreTargetCommandHandler(uow.Object, CurrentUser());
        var result  = await handler.Handle(new ValidateDatabaseRestoreTargetCommand(Guid.NewGuid(), "my_restore"), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("404");
    }

    [Theory]
    [InlineData("a")]              // too short (< 2 chars)
    [InlineData("has spaces")]
    [InlineData("has.dots")]
    [InlineData("has@symbol")]
    [InlineData("has/slash")]
    public async Task ValidateRestoreTarget_InvalidName_ReturnsIsValidFalse(string badName)
    {
        var (uow, dbRepo, _) = MakeUow();
        var db = MakeDb();
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        uow.Setup(x => x.Databases.GetByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<DatabaseInstance>());

        var handler = new ValidateDatabaseRestoreTargetCommandHandler(uow.Object, CurrentUser());
        var result  = await handler.Handle(new ValidateDatabaseRestoreTargetCommand(db.Id, badName), default);

        result.IsSuccess.Should().BeTrue("handler itself succeeds; validation outcome is in Value");
        result.Value!.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRestoreTarget_NameAlreadyExistsOnSameServer_ReturnsIsValidFalse()
    {
        var (uow, dbRepo, _) = MakeUow();
        var sharedServerId = Guid.NewGuid();
        var db = MakeDb();
        db.ServerId = sharedServerId;

        var sibling = MakeDb();
        sibling.ServerId     = sharedServerId;
        sibling.DatabaseName = "conflict_db";

        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        uow.Setup(x => x.Databases.GetByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<DatabaseInstance> { db, sibling });

        var handler = new ValidateDatabaseRestoreTargetCommandHandler(uow.Object, CurrentUser());
        var result  = await handler.Handle(new ValidateDatabaseRestoreTargetCommand(db.Id, "conflict_db"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeFalse();
        result.Value.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task ValidateRestoreTarget_ValidUniqueName_ReturnsIsValidTrue()
    {
        var (uow, dbRepo, _) = MakeUow();
        var db = MakeDb();
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        uow.Setup(x => x.Databases.GetByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<DatabaseInstance>());

        var handler = new ValidateDatabaseRestoreTargetCommandHandler(uow.Object, CurrentUser());
        var result  = await handler.Handle(new ValidateDatabaseRestoreTargetCommand(db.Id, "my_restore_v2"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value.NormalizedTargetDatabaseName.Should().Be("my_restore_v2");
    }

    [Fact]
    public async Task ValidateRestoreTarget_NullInput_DefaultsToDbNameSuffix()
    {
        var (uow, dbRepo, _) = MakeUow();
        var db = MakeDb();
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        uow.Setup(x => x.Databases.GetByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<DatabaseInstance>());

        var handler = new ValidateDatabaseRestoreTargetCommandHandler(uow.Object, CurrentUser());
        var result  = await handler.Handle(new ValidateDatabaseRestoreTargetCommand(db.Id, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value.NormalizedTargetDatabaseName.Should().Be("testdb_restore");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // C. StartDatabaseRestoreCommandHandler
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task StartRestore_DatabaseNotFound_Returns404()
    {
        var (uow, dbRepo, _) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        dbRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((DatabaseInstance?)null);

        var handler = new StartDatabaseRestoreCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new StartDatabaseRestoreCommand(Guid.NewGuid(), Guid.NewGuid(), "target"), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("404");
    }

    [Fact]
    public async Task StartRestore_DbStatusRestoring_Returns409()
    {
        var (uow, dbRepo, _) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb(status: DatabaseInstanceStatus.Restoring);
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);

        var handler = new StartDatabaseRestoreCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new StartDatabaseRestoreCommand(db.Id, Guid.NewGuid(), "target"), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("409");
    }

    [Fact]
    public async Task StartRestore_ActiveServiceJob_Returns409()
    {
        var (uow, dbRepo, _) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb();
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.HasActiveRestoreAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new StartDatabaseRestoreCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new StartDatabaseRestoreCommand(db.Id, Guid.NewGuid(), "target"), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("409");
    }

    [Fact]
    public async Task StartRestore_BackupNotFound_Returns404()
    {
        var (uow, dbRepo, backupRepo) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb();
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.HasActiveRestoreAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        backupRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((DatabaseBackup?)null);

        var handler = new StartDatabaseRestoreCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new StartDatabaseRestoreCommand(db.Id, Guid.NewGuid(), "target"), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("404");
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("failed")]
    [InlineData("running")]
    public async Task StartRestore_BackupNotCompleted_Returns409(string backupStatus)
    {
        var (uow, dbRepo, backupRepo) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb();
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.HasActiveRestoreAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var backup = new DatabaseBackup { DatabaseInstanceId = db.Id, Status = backupStatus, FileName = "b.bak" };
        backupRepo.Setup(x => x.GetByIdAsync(backup.Id, It.IsAny<CancellationToken>())).ReturnsAsync(backup);

        var handler = new StartDatabaseRestoreCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new StartDatabaseRestoreCommand(db.Id, backup.Id, "target"), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("409");
        result.Error.Should().Contain("completed");
    }

    [Theory]
    [InlineData("bad name")]      // space
    [InlineData("has.dot")]       // dot
    [InlineData("a")]             // too short
    public async Task StartRestore_InvalidTargetName_Returns400(string badTarget)
    {
        var (uow, dbRepo, backupRepo) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb();
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.HasActiveRestoreAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var backup = new DatabaseBackup { DatabaseInstanceId = db.Id, Status = "completed", FileName = "ok.bak" };
        backupRepo.Setup(x => x.GetByIdAsync(backup.Id, It.IsAny<CancellationToken>())).ReturnsAsync(backup);

        var handler = new StartDatabaseRestoreCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new StartDatabaseRestoreCommand(db.Id, backup.Id, badTarget), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("400");
    }

    [Fact]
    public async Task StartRestore_ValidRequest_ReturnsQueuedJobAndSetsRestoringStatus()
    {
        var (uow, dbRepo, backupRepo) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb();
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.HasActiveRestoreAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var backup = new DatabaseBackup { DatabaseInstanceId = db.Id, Status = "completed", FileName = "ok.bak" };
        backupRepo.Setup(x => x.GetByIdAsync(backup.Id, It.IsAny<CancellationToken>())).ReturnsAsync(backup);

        var expectedJob = new DatabaseRestoreJobDto(
            Guid.NewGuid(), db.Id, backup.Id, "queued", 0, "Restore job queued", "my_restore", DateTime.UtcNow, null);
        restoreJobs.Setup(x => x.StartAsync(db.Id, backup.Id, "my_restore", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expectedJob);

        var handler = new StartDatabaseRestoreCommandHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new StartDatabaseRestoreCommand(db.Id, backup.Id, "my_restore"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("queued");
        result.Value.JobId.Should().Be(expectedJob.JobId);
        db.Status.Should().Be(DatabaseInstanceStatus.Restoring, "handler must transition DB to Restoring");
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // D. GetDatabaseRestoreJobQueryHandler — progress polling
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PollRestoreJob_DatabaseNotFound_Returns404()
    {
        var (uow, dbRepo, _) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        dbRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((DatabaseInstance?)null);

        var handler = new GetDatabaseRestoreJobQueryHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new GetDatabaseRestoreJobQuery(Guid.NewGuid(), Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("404");
    }

    [Fact]
    public async Task PollRestoreJob_JobNotFound_Returns404()
    {
        var (uow, dbRepo, _) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db = MakeDb();
        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.GetAsync(db.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((DatabaseRestoreJobDto?)null);

        var handler = new GetDatabaseRestoreJobQueryHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new GetDatabaseRestoreJobQuery(db.Id, Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("404");
    }

    [Theory]
    [InlineData("queued",     0)]
    [InlineData("running",   45)]
    [InlineData("completed", 100)]
    [InlineData("failed",     0)]
    public async Task PollRestoreJob_ReturnsCurrentStatusAndProgress(string jobStatus, int progress)
    {
        var (uow, dbRepo, _) = MakeUow();
        var restoreJobs = new Mock<IDatabaseRestoreJobService>();
        var db    = MakeDb();
        var jobId = Guid.NewGuid();
        var job   = new DatabaseRestoreJobDto(
            jobId, db.Id, Guid.NewGuid(), jobStatus, progress, $"Status: {jobStatus}", "my_restore",
            DateTime.UtcNow, jobStatus == "completed" ? DateTime.UtcNow : null);

        dbRepo.Setup(x => x.GetByIdAsync(db.Id, It.IsAny<CancellationToken>())).ReturnsAsync(db);
        restoreJobs.Setup(x => x.GetAsync(db.Id, jobId, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        var handler = new GetDatabaseRestoreJobQueryHandler(uow.Object, CurrentUser(), restoreJobs.Object);
        var result  = await handler.Handle(new GetDatabaseRestoreJobQuery(db.Id, jobId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(jobStatus);
        result.Value.ProgressPercent.Should().Be(progress);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // E. TestS3DestinationConnectionCommandHandler
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TestS3_ServiceReturnsFailure_HandlerReturns400Failure()
    {
        var s3Svc = new Mock<IS3DestinationValidationService>();
        s3Svc.Setup(x => x.TestConnectionAsync(It.IsAny<S3DestinationTestRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new S3DestinationTestResult(false, "Cannot reach endpoint.", 0, "https://s3.us-east-1.amazonaws.com"));

        var request = new S3DestinationTestRequest("prod-backup", "aws", null, "my-bucket", "us-east-1", "AKID", "SECRET", null);
        var handler = new TestS3DestinationConnectionCommandHandler(s3Svc.Object);
        var result  = await handler.Handle(new TestS3DestinationConnectionCommand(request), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("400");
        result.Error.Should().Contain("Cannot reach endpoint");
    }

    [Fact]
    public async Task TestS3_ServiceReturnsSuccess_HandlerPropagatesDetails()
    {
        var s3Svc = new Mock<IS3DestinationValidationService>();
        s3Svc.Setup(x => x.TestConnectionAsync(It.IsAny<S3DestinationTestRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new S3DestinationTestResult(true, "Connection successful.", 38, "https://s3.us-east-1.amazonaws.com"));

        var request = new S3DestinationTestRequest("prod-backup", "aws", null, "my-bucket", "us-east-1", "AKID", "SECRET", null);
        var handler = new TestS3DestinationConnectionCommandHandler(s3Svc.Object);
        var result  = await handler.Handle(new TestS3DestinationConnectionCommand(request), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.LatencyMs.Should().Be(38);
        result.Value.ResolvedEndpoint.Should().Contain("amazonaws.com");
        result.Value.Message.Should().Contain("successful");
    }

    [Fact]
    public async Task TestS3_ServiceCalledWithExactRequest()
    {
        var s3Svc = new Mock<IS3DestinationValidationService>();
        s3Svc.Setup(x => x.TestConnectionAsync(It.IsAny<S3DestinationTestRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new S3DestinationTestResult(true, "OK", 10, "https://example.com"));

        var request = new S3DestinationTestRequest("r2-backup", "cloudflare-r2", "https://custom.r2.dev", "backup-bucket", null, "KEYID", "KEYSECRET", "db/");
        var handler = new TestS3DestinationConnectionCommandHandler(s3Svc.Object);
        await handler.Handle(new TestS3DestinationConnectionCommand(request), default);

        s3Svc.Verify(
            x => x.TestConnectionAsync(
                It.Is<S3DestinationTestRequest>(r =>
                    r.Name == "r2-backup" &&
                    r.Provider == "cloudflare-r2" &&
                    r.Bucket == "backup-bucket" &&
                    r.PathPrefix == "db/"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
