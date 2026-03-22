using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Projects.Commands;
using DeployFlow.Application.Features.Projects.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Route("api/projects")]
[Authorize]
public class ProjectsController : BaseController
{
    public ProjectsController(IMediator mediator) : base(mediator)
    {
        
    }

    /// <summary>List all projects for the authenticated tenant.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetProjectsQuery(page, pageSize, search, status), ct);
        return ToResponse(result);
    }

    /// <summary>Get a single project by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetProjectQuery(id), ct);
        return ToResponse(result);
    }

    /// <summary>Create a new project.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new CreateProjectCommand(
            request.Name, request.Description, request.RepositoryUrl, request.Branch,
            request.BuildCommand, request.StartCommand, request.InstallCommand,
            request.DockerfilePath, request.Framework, request.CustomDomain,
            request.AutoDeploy, request.Tags, request.AssignedServerId), ct);

        if (!result.IsSuccess) return ToResponse(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Update an existing project.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new UpdateProjectCommand(
            id, request.Name, request.Description, request.RepositoryUrl, request.Branch,
            request.BuildCommand, request.StartCommand, request.InstallCommand,
            request.DockerfilePath, null, request.CustomDomain, request.AutoDeploy,
            request.Tags, request.AssignedServerId), ct);
        return ToResponse(result);
    }

    /// <summary>Delete (archive) a project.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new DeleteProjectCommand(id), ct);
        return ToResponse(result);
    }
}
