using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Templates;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record PolicyTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    string AppliesTo,
    int RequiredApprovals,
    string? RequiredApproverRole,
    string? AutoApprovePattern,
    string? AllowedHoursUtc,
    bool IsEnabled,
    Guid? ProjectId,
    DateTime CreatedAt
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetPolicyTemplatesQuery(Guid? ProjectId) : IRequest<Result<List<PolicyTemplateDto>>>;

public class GetPolicyTemplatesQueryHandler
    : IRequestHandler<GetPolicyTemplatesQuery, Result<List<PolicyTemplateDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetPolicyTemplatesQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result<List<PolicyTemplateDto>>> Handle(
        GetPolicyTemplatesQuery request, CancellationToken ct)
    {
        var all = await _uow.PolicyTemplates.GetByTenantAsync(_currentUser.TenantId, ct);

        var filtered = request.ProjectId.HasValue
            ? all.Where(p => p.ProjectId == null || p.ProjectId == request.ProjectId.Value)
            : all.Where(p => p.ProjectId == null);

        return Result<List<PolicyTemplateDto>>.Success(
            filtered.OrderBy(p => p.Name).Select(Map).ToList());
    }

    internal static PolicyTemplateDto Map(PolicyTemplate p) => new(
        p.Id, p.Name, p.Description, p.AppliesTo, p.RequiredApprovals,
        p.RequiredApproverRole, p.AutoApprovePattern, p.AllowedHoursUtc,
        p.IsEnabled, p.ProjectId, p.CreatedAt);
}

// ── Create ────────────────────────────────────────────────────────────────────

public record CreatePolicyTemplateCommand(
    string Name,
    string? Description,
    string AppliesTo,
    int RequiredApprovals,
    string? RequiredApproverRole,
    string? AutoApprovePattern,
    string? AllowedHoursUtc,
    Guid? ProjectId
) : IRequest<Result<PolicyTemplateDto>>;

public class CreatePolicyTemplateCommandHandler
    : IRequestHandler<CreatePolicyTemplateCommand, Result<PolicyTemplateDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreatePolicyTemplateCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result<PolicyTemplateDto>> Handle(
        CreatePolicyTemplateCommand request, CancellationToken ct)
    {
        var policy = new PolicyTemplate
        {
            TenantId              = _currentUser.TenantId,
            Name                  = request.Name,
            Description           = request.Description,
            AppliesTo             = request.AppliesTo,
            RequiredApprovals     = Math.Max(1, request.RequiredApprovals),
            RequiredApproverRole  = request.RequiredApproverRole,
            AutoApprovePattern    = request.AutoApprovePattern,
            AllowedHoursUtc       = request.AllowedHoursUtc,
            IsEnabled             = true,
            ProjectId             = request.ProjectId,
        };

        await _uow.PolicyTemplates.AddAsync(policy, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<PolicyTemplateDto>.Success(GetPolicyTemplatesQueryHandler.Map(policy));
    }
}

// ── Update ────────────────────────────────────────────────────────────────────

public record UpdatePolicyTemplateCommand(
    Guid Id,
    string? Name,
    string? Description,
    string? AppliesTo,
    int? RequiredApprovals,
    string? RequiredApproverRole,
    string? AutoApprovePattern,
    string? AllowedHoursUtc,
    bool? IsEnabled
) : IRequest<Result<PolicyTemplateDto>>;

public class UpdatePolicyTemplateCommandHandler
    : IRequestHandler<UpdatePolicyTemplateCommand, Result<PolicyTemplateDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpdatePolicyTemplateCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result<PolicyTemplateDto>> Handle(
        UpdatePolicyTemplateCommand request, CancellationToken ct)
    {
        var policy = await _uow.PolicyTemplates.GetByIdAsync(request.Id, ct);
        if (policy is null || policy.TenantId != _currentUser.TenantId)
            return Result<PolicyTemplateDto>.Failure("Policy template not found.", 404);

        if (request.Name is not null)             policy.Name                 = request.Name;
        if (request.Description is not null)      policy.Description          = request.Description;
        if (request.AppliesTo is not null)        policy.AppliesTo            = request.AppliesTo;
        if (request.RequiredApprovals.HasValue)   policy.RequiredApprovals    = Math.Max(1, request.RequiredApprovals.Value);
        if (request.RequiredApproverRole is not null) policy.RequiredApproverRole = request.RequiredApproverRole;
        if (request.AutoApprovePattern is not null)   policy.AutoApprovePattern   = request.AutoApprovePattern;
        if (request.AllowedHoursUtc is not null)  policy.AllowedHoursUtc     = request.AllowedHoursUtc;
        if (request.IsEnabled.HasValue)           policy.IsEnabled            = request.IsEnabled.Value;

        await _uow.SaveChangesAsync(ct);
        return Result<PolicyTemplateDto>.Success(GetPolicyTemplatesQueryHandler.Map(policy));
    }
}

// ── Delete ────────────────────────────────────────────────────────────────────

public record DeletePolicyTemplateCommand(Guid Id) : IRequest<Result>;

public class DeletePolicyTemplateCommandHandler
    : IRequestHandler<DeletePolicyTemplateCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeletePolicyTemplateCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    { _uow = uow; _currentUser = currentUser; }

    public async Task<Result> Handle(DeletePolicyTemplateCommand request, CancellationToken ct)
    {
        var policy = await _uow.PolicyTemplates.GetByIdAsync(request.Id, ct);
        if (policy is null || policy.TenantId != _currentUser.TenantId)
            return Result.Failure("Policy template not found.", 404);

        await _uow.PolicyTemplates.DeleteAsync(policy, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ── Policy Evaluation Helper ──────────────────────────────────────────────────

public static class PolicyEvaluator
{
    /// <summary>
    /// Checks whether a deployment requires approval based on active policy templates.
    /// Returns the minimum number of approvals required (0 = no approval needed).
    /// </summary>
    public static async Task<int> GetRequiredApprovalsAsync(
        IUnitOfWork uow, Deployment deployment, Guid tenantId,
        string? environmentSlug, CancellationToken ct)
    {
        var policies = await uow.PolicyTemplates.GetByTenantAsync(tenantId, ct);
        int max = 0;

        foreach (var p in policies.Where(p => p.IsEnabled &&
            (p.ProjectId == null || p.ProjectId == deployment.ProjectId)))
        {
            // Check environment match
            var envSlugs = p.AppliesTo.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (environmentSlug is not null && !envSlugs.Any(s =>
                    s.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals(environmentSlug, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Check auto-approve pattern
            if (!string.IsNullOrWhiteSpace(p.AutoApprovePattern) &&
                !string.IsNullOrWhiteSpace(deployment.Branch) &&
                System.Text.RegularExpressions.Regex.IsMatch(deployment.Branch, p.AutoApprovePattern))
                continue; // auto-approved for this policy

            // Check time restriction
            if (!string.IsNullOrWhiteSpace(p.AllowedHoursUtc))
            {
                var parts = p.AllowedHoursUtc.Split('-');
                if (parts.Length == 2 &&
                    TimeOnly.TryParse(parts[0], out var from) &&
                    TimeOnly.TryParse(parts[1], out var to))
                {
                    var now = TimeOnly.FromDateTime(DateTime.UtcNow);
                    if (now < from || now > to)
                        continue; // outside allowed hours — block by requiring more approvals
                }
            }

            max = Math.Max(max, p.RequiredApprovals);
        }

        return max;
    }
}
