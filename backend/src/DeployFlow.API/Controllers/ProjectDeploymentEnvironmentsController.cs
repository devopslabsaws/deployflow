using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/projects/{projectId:guid}/deployment-environments")]
public class ProjectDeploymentEnvironmentsController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly FeatureFlagsOptions _featureFlags;

    public ProjectDeploymentEnvironmentsController(
        MediatR.IMediator mediator,
        ApplicationDbContext db,
        ICurrentUser currentUser,
        IOptions<FeatureFlagsOptions> featureFlags) : base(mediator)
    {
        _db = db;
        _currentUser = currentUser;
        _featureFlags = featureFlags.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == _currentUser.TenantId, ct);
        if (project is null) return NotFound(new { error = "Project not found." });

        if (!_featureFlags.MultiEnvironmentEnabled)
        {
            return Ok(new[]
            {
                new
                {
                    id = Guid.Empty,
                    projectId,
                    environmentName = "Prod",
                    branch = project.RepositoryBranch ?? "main",
                    autoDeploy = project.AutoDeployEnabled,
                    requiresApproval = false,
                    isDefault = true,
                    order = 1,
                }
            });
        }

        var items = await _db.ProjectDeploymentEnvironments
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.TenantId == _currentUser.TenantId && !x.IsDeleted)
            .OrderBy(x => x.Order)
            .ToListAsync(ct);

        return Ok(items.Select(Map));
    }

    [HttpPut("bulk")]
    public async Task<IActionResult> UpsertBulk(Guid projectId, [FromBody] UpsertProjectDeploymentEnvironmentsRequest request, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == _currentUser.TenantId, ct);
        if (project is null) return NotFound(new { error = "Project not found." });

        if (!_featureFlags.MultiEnvironmentEnabled)
        {
            return Ok(new
            {
                items = new[]
                {
                    new
                    {
                        id = Guid.Empty,
                        projectId,
                        environmentName = "Prod",
                        branch = project.RepositoryBranch ?? "main",
                        autoDeploy = project.AutoDeployEnabled,
                        requiresApproval = false,
                        isDefault = true,
                        order = 1,
                    }
                }
            });
        }

        var existing = await _db.ProjectDeploymentEnvironments
            .Where(x => x.ProjectId == projectId && x.TenantId == _currentUser.TenantId && !x.IsDeleted)
            .ToListAsync(ct);

        _db.ProjectDeploymentEnvironments.RemoveRange(existing);

        var clean = (request.Items ?? new List<ProjectDeploymentEnvironmentItem>())
            .Where(i => !string.IsNullOrWhiteSpace(i.EnvironmentName) && !string.IsNullOrWhiteSpace(i.Branch))
            .Select((i, idx) => new ProjectDeploymentEnvironment
            {
                TenantId = _currentUser.TenantId,
                ProjectId = projectId,
                EnvironmentName = i.EnvironmentName.Trim(),
                Branch = i.Branch.Trim(),
                AutoDeploy = i.AutoDeploy,
                RequiresApproval = i.RequiresApproval,
                IsDefault = i.IsDefault,
                Order = i.Order > 0 ? i.Order : idx + 1,
            })
            .ToList();

        if (clean.Count == 0)
        {
            clean.Add(new ProjectDeploymentEnvironment
            {
                TenantId = _currentUser.TenantId,
                ProjectId = projectId,
                EnvironmentName = "Prod",
                Branch = project.RepositoryBranch ?? "main",
                AutoDeploy = true,
                RequiresApproval = false,
                IsDefault = true,
                Order = 1,
            });
        }

        // Ensure a single default row
        if (!clean.Any(x => x.IsDefault))
            clean[0].IsDefault = true;

        var defaultCount = clean.Count(x => x.IsDefault);
        if (defaultCount > 1)
        {
            var first = true;
            foreach (var row in clean.Where(x => x.IsDefault))
            {
                if (first) { first = false; continue; }
                row.IsDefault = false;
            }
        }

        await _db.ProjectDeploymentEnvironments.AddRangeAsync(clean, ct);
        await _db.SaveChangesAsync(ct);

        var result = clean.OrderBy(x => x.Order).Select(Map).ToList();
        return Ok(new { items = result });
    }

    private static object Map(ProjectDeploymentEnvironment x) => new
    {
        id = x.Id,
        projectId = x.ProjectId,
        environmentName = x.EnvironmentName,
        branch = x.Branch,
        autoDeploy = x.AutoDeploy,
        requiresApproval = x.RequiresApproval,
        isDefault = x.IsDefault,
        order = x.Order,
    };
}

public record ProjectDeploymentEnvironmentItem(
    string EnvironmentName,
    string Branch,
    bool AutoDeploy,
    bool RequiresApproval,
    bool IsDefault,
    int Order
);

public record UpsertProjectDeploymentEnvironmentsRequest(List<ProjectDeploymentEnvironmentItem> Items);
