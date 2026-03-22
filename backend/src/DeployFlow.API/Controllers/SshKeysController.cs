using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.SshKeys;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/ssh-keys")]
public class SshKeysController : BaseController
{
    public SshKeysController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetKeys(CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetSshKeysQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSshKeyRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new CreateSshKeyCommand(
            request.Name, request.PrivateKey, request.Passphrase), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteSshKeyCommand(id), ct));
}
