using DeployFlow.Application.Features.ApiKeys;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/api-keys")]
[Tags("API Keys")]
public class ApiKeysController : BaseController
{
    public ApiKeysController(IMediator mediator) : base(mediator) { }

    /// <summary>List all API keys for the current user's tenant.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => ToResponse(await Mediator.Send(new GetApiKeysQuery(), ct));

    /// <summary>Create a new API key. The full key is returned only once.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest request, CancellationToken ct)
        => ToResponse(await Mediator.Send(new CreateApiKeyCommand(request.Name, request.Permissions ?? "read"), ct));

    /// <summary>Delete / revoke an API key.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await Mediator.Send(new DeleteApiKeyCommand(id), ct));
}

public record CreateApiKeyRequest(string Name, string? Permissions = "read");
