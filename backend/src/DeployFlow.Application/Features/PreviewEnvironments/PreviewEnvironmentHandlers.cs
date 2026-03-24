using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.PreviewEnvironments;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record PreviewEnvironmentDto(
    Guid Id,
    Guid ProjectId,
    string PrNumber,
    string PrTitle,
    string Branch,
    string? Url,
    string Status,
    Guid? DeploymentId,
    DateTime? MergedAt,
    DateTime? ClosedAt,
    DateTime CreatedAt
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetPreviewEnvironmentsQuery(Guid? ProjectId) : IRequest<Result<List<PreviewEnvironmentDto>>>;

public class GetPreviewEnvironmentsQueryHandler
    : IRequestHandler<GetPreviewEnvironmentsQuery, Result<List<PreviewEnvironmentDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetPreviewEnvironmentsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<PreviewEnvironmentDto>>> Handle(
        GetPreviewEnvironmentsQuery request, CancellationToken ct)
    {
        var previews = await _uow.PreviewEnvironments.GetByTenantAsync(_currentUser.TenantId, ct);

        var filtered = request.ProjectId.HasValue
            ? previews.Where(p => p.ProjectId == request.ProjectId.Value)
            : previews;

        return Result<List<PreviewEnvironmentDto>>.Success(
            filtered.OrderByDescending(p => p.CreatedAt).Select(Map).ToList());
    }

    internal static PreviewEnvironmentDto Map(PreviewEnvironment p) => new(
        p.Id, p.ProjectId, p.PrNumber, p.PrTitle, p.Branch,
        p.Url, p.Status, p.DeploymentId, p.MergedAt, p.ClosedAt, p.CreatedAt);
}

// ── Create (triggered by Git webhook on PR open/sync) ─────────────────────────

public record CreatePreviewEnvironmentCommand(
    Guid ProjectId,
    string PrNumber,
    string PrTitle,
    string Branch
) : IRequest<Result<PreviewEnvironmentDto>>;

public class CreatePreviewEnvironmentCommandHandler
    : IRequestHandler<CreatePreviewEnvironmentCommand, Result<PreviewEnvironmentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreatePreviewEnvironmentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<PreviewEnvironmentDto>> Handle(
        CreatePreviewEnvironmentCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<PreviewEnvironmentDto>.Failure("Project not found.", "404");

        // Check for existing preview for same PR
        var existing = (await _uow.PreviewEnvironments
            .GetByTenantAsync(_currentUser.TenantId, ct))
            .FirstOrDefault(p => p.ProjectId == request.ProjectId && p.PrNumber == request.PrNumber);

        if (existing is not null)
        {
            // Update to "building" on re-push
            existing.Status = "building";
            await _uow.PreviewEnvironments.UpdateAsync(existing, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<PreviewEnvironmentDto>.Success(GetPreviewEnvironmentsQueryHandler.Map(existing));
        }

        // Generate deterministic preview URL from branch slug
        var branchSlug = request.Branch
            .ToLowerInvariant()
            .Replace('/', '-')
            .Replace('_', '-');
        var previewUrl = $"https://pr-{request.PrNumber}-{branchSlug.Take(20)}.preview.yourplatform.dev";

        var preview = new PreviewEnvironment
        {
            TenantId = _currentUser.TenantId,
            ProjectId = request.ProjectId,
            PrNumber = request.PrNumber,
            PrTitle = request.PrTitle,
            Branch = request.Branch,
            Url = previewUrl,
            Status = "building"
        };

        await _uow.PreviewEnvironments.AddAsync(preview, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<PreviewEnvironmentDto>.Success(GetPreviewEnvironmentsQueryHandler.Map(preview));
    }
}

// ── Update Status ─────────────────────────────────────────────────────────────

public record UpdatePreviewStatusCommand(Guid Id, string Status, string? Url) : IRequest<Result<bool>>;

public class UpdatePreviewStatusCommandHandler : IRequestHandler<UpdatePreviewStatusCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpdatePreviewStatusCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpdatePreviewStatusCommand request, CancellationToken ct)
    {
        var preview = await _uow.PreviewEnvironments.GetByIdAsync(request.Id, ct);
        if (preview is null || preview.TenantId != _currentUser.TenantId)
            return Result<bool>.Failure("Preview environment not found.", "404");

        preview.Status = request.Status;
        if (request.Url is not null)
            preview.Url = request.Url;

        if (request.Status is "merged")
            preview.MergedAt = DateTime.UtcNow;
        else if (request.Status is "closed")
            preview.ClosedAt = DateTime.UtcNow;

        await _uow.PreviewEnvironments.UpdateAsync(preview, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ── Cleanup after PR merge/close ─────────────────────────────────────────────

public record CleanupPreviewEnvironmentCommand(Guid Id) : IRequest<Result<bool>>;

public class CleanupPreviewEnvironmentCommandHandler
    : IRequestHandler<CleanupPreviewEnvironmentCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CleanupPreviewEnvironmentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(CleanupPreviewEnvironmentCommand request, CancellationToken ct)
    {
        var preview = await _uow.PreviewEnvironments.GetByIdAsync(request.Id, ct);
        if (preview is null || preview.TenantId != _currentUser.TenantId)
            return Result<bool>.Failure("Preview environment not found.", "404");

        await _uow.PreviewEnvironments.DeleteAsync(preview, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
