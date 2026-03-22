using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Domains;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/domains")]
public class DomainsController : BaseController
{
    public DomainsController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetDomains(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetDomainsQuery(page, pageSize), ct));

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddDomainRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new AddDomainCommand(
            request.DomainName, request.SslEnabled, request.ServiceId), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteDomainCommand(id), ct));
}

public record AddDomainRequest(
    string DomainName,
    bool SslEnabled,
    Guid? ServiceId
);
