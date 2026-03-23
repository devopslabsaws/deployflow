using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Databases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/databases")]
public class DatabasesController : BaseController
{
    public DatabasesController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetDatabases(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? serverId = null,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetDatabasesQuery(page, pageSize, serverId), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetDatabaseByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDatabaseRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new CreateDatabaseCommand(
            request.Name, request.Engine, request.Version, request.DatabaseName,
            request.Username, request.Password, request.StorageGb, request.AutoBackup,
            request.BackupSchedule, request.ServerId), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteDatabaseCommand(id), ct));

    [HttpGet("{id:guid}/backups")]
    public async Task<IActionResult> GetBackups(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetDatabaseBackupsQuery(id), ct));

    [HttpPost("{id:guid}/backup")]
    public async Task<IActionResult> TriggerBackup(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new TriggerDatabaseBackupCommand(id), ct));

    // ─── Restore (legacy validation — kept for backward compat) ────────────

    [HttpPost("{id:guid}/restore/validate-target")]
    public async Task<IActionResult> ValidateRestoreTarget(
        Guid id,
        [FromBody] ValidateRestoreTargetRequest request,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new ValidateDatabaseRestoreTargetCommand(id, request.TargetDatabaseName), ct));

    // ─── S3 Destinations ─────────────────────────────────────────────────────

    [HttpGet("s3-destinations")]
    public async Task<IActionResult> GetS3Destinations(CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetS3DestinationsQuery(), ct));

    [HttpGet("s3-destinations/{id:guid}")]
    public async Task<IActionResult> GetS3Destination(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetS3DestinationByIdQuery(id), ct));

    [HttpPost("s3-destinations")]
    public async Task<IActionResult> CreateS3Destination(
        [FromBody] CreateS3DestinationRequest request,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new CreateS3DestinationCommand(
            request.Name, request.Description, request.Endpoint, request.BucketName,
            request.AccessKeyId, request.SecretAccessKey, request.Region, request.IsDefault), ct));

    [HttpPut("s3-destinations/{id:guid}")]
    public async Task<IActionResult> UpdateS3Destination(
        Guid id,
        [FromBody] UpdateS3DestinationRequest request,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new UpdateS3DestinationCommand(
            id, request.Name, request.Description, request.Endpoint, request.BucketName,
            request.AccessKeyId, request.SecretAccessKey, request.Region, request.IsDefault), ct));

    [HttpDelete("s3-destinations/{id:guid}")]
    public async Task<IActionResult> DeleteS3Destination(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteS3DestinationCommand(id), ct));

    [HttpPost("s3-destinations/test")]
    public async Task<IActionResult> TestS3Destination(
        [FromBody] S3DestinationTestRequest request,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new TestS3DestinationCommand(request), ct));

    // ─── Backup Policies ─────────────────────────────────────────────────────

    [HttpGet("{id:guid}/backup-policy")]
    public async Task<IActionResult> GetBackupPolicy(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetBackupPolicyQuery(id), ct));

    [HttpPut("{id:guid}/backup-policy")]
    public async Task<IActionResult> UpdateBackupPolicy(
        Guid id,
        [FromBody] UpdateBackupPolicyRequest request,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new UpdateBackupPolicyCommand(
            id, request.IsEnabled, request.CronExpression, request.RetentionDays,
            request.S3DestinationId, request.StorageLocation), ct));

    // ─── Restore Jobs ────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/restore/jobs")]
    public async Task<IActionResult> GetRestoreJobs(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetRestoreJobsQuery(id), ct));

    [HttpPost("{id:guid}/restore/jobs")]
    public async Task<IActionResult> CreateRestoreJob(
        Guid id,
        [FromBody] StartDatabaseRestoreRequest request,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new CreateRestoreJobCommand(
            id, request.BackupId, request.TargetDatabaseName ?? $"restored_{Guid.NewGuid().ToString()[..8]}"), ct));

    [HttpGet("{id:guid}/restore/jobs/{jobId:guid}")]
    public async Task<IActionResult> GetRestoreJob(Guid id, Guid jobId, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetRestoreJobQuery(id, jobId), ct));
}
