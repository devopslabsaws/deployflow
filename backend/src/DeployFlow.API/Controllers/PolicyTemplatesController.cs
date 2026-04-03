using DeployFlow.Application.Features.Templates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/policy-templates")]
public class PolicyTemplatesController : BaseController
{
    public PolicyTemplatesController(IMediator mediator) : base(mediator) { }

    /// <summary>List all policy templates (tenant-wide or project-scoped).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? projectId, CancellationToken ct)
        => ToResponse(await Mediator.Send(new GetPolicyTemplatesQuery(projectId), ct));

    /// <summary>Create a new policy template.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePolicyTemplateRequest req, CancellationToken ct)
    {
        var result = await Mediator.Send(new CreatePolicyTemplateCommand(
            req.Name, req.Description, req.AppliesTo ?? "production",
            req.RequiredApprovals > 0 ? req.RequiredApprovals : 1,
            req.RequiredApproverRole, req.AutoApprovePattern,
            req.AllowedHoursUtc, req.ProjectId), ct);

        if (!result.IsSuccess) return ToResponse(result);
        return CreatedAtAction(nameof(GetAll), result.Value);
    }

    /// <summary>Update a policy template.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePolicyTemplateRequest req, CancellationToken ct)
        => ToResponse(await Mediator.Send(new UpdatePolicyTemplateCommand(
            id, req.Name, req.Description, req.AppliesTo,
            req.RequiredApprovals, req.RequiredApproverRole,
            req.AutoApprovePattern, req.AllowedHoursUtc, req.IsEnabled), ct));

    /// <summary>Delete a policy template.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await Mediator.Send(new DeletePolicyTemplateCommand(id), ct));
}

public record CreatePolicyTemplateRequest(
    string Name,
    string? Description,
    string? AppliesTo,
    int RequiredApprovals,
    string? RequiredApproverRole,
    string? AutoApprovePattern,
    string? AllowedHoursUtc,
    Guid? ProjectId
);

public record UpdatePolicyTemplateRequest(
    string? Name,
    string? Description,
    string? AppliesTo,
    int? RequiredApprovals,
    string? RequiredApproverRole,
    string? AutoApprovePattern,
    string? AllowedHoursUtc,
    bool? IsEnabled
);
