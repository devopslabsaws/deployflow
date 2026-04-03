using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Servers.Commands;
using DeployFlow.Application.Features.Servers.Queries;
using DeployFlow.Domain.Interfaces;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.API.Controllers;

[Route("api/servers")]
[Authorize]
public class ServersController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly ISshService _ssh;
    private readonly IEncryptionService _enc;
    private readonly ICurrentUser _currentUser;

    public ServersController(
        IMediator mediator,
        ApplicationDbContext db,
        IUnitOfWork uow,
        ISshService ssh,
        IEncryptionService enc,
        ICurrentUser currentUser)
        : base(mediator)
    {
        _db = db;
        _uow = uow;
        _ssh = ssh;
        _enc = enc;
        _currentUser = currentUser;
    }

    /// <summary>List all servers.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? provider = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetServersQuery(page, pageSize, status, provider), ct);
        return ToResponse(result);
    }

    /// <summary>Get server details.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetServerQuery(id), ct);
        return ToResponse(result);
    }

    /// <summary>Get historical metrics for a server.</summary>
    [HttpGet("{id:guid}/metrics")]
    public async Task<IActionResult> GetMetrics(Guid id, [FromQuery] int hours = 24, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetServerMetricsQuery(id, hours), ct);
        return ToResponse(result);
    }

    /// <summary>Add a new server.</summary>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddServerRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new AddServerCommand(
            request.Name, request.IpAddress, request.SshPort, request.SshUser,
            request.SshKeyId, request.Provider, request.Region,
            request.CpuCores, request.MemoryGb, request.DiskGb), ct);

        if (!result.IsSuccess) return ToResponse(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Update server settings (name, SSH user/port/key, region).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServerRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new UpdateServerCommand(
            id, request.Name, request.SshKeyId, request.SshPort, request.SshUser, request.Region), ct);
        return ToResponse(result);
    }

    /// <summary>Test SSH connectivity to a server.</summary>
    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new TestServerConnectionCommand(id), ct);
        return ToResponse(result);
    }

    /// <summary>Execute a command on the server via SSH.</summary>
    [HttpPost("{id:guid}/exec")]
    public async Task<IActionResult> Exec(Guid id, [FromBody] ExecRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new ExecServerCommand(id, request.Command), ct);
        return ToResponse(result);
    }

    /// <summary>Remove a server.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new DeleteServerCommand(id), ct);
        return ToResponse(result);
    }

    // ── Docker Cleanup ────────────────────────────────────────────────────────

    /// <summary>Get Docker cleanup configuration for a server.</summary>
    [HttpGet("{id:guid}/docker-cleanup")]
    public async Task<IActionResult> GetDockerCleanup(Guid id, CancellationToken ct)
    {
        var server = await _db.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (server is null) return NotFound();
        return Ok(new
        {
            server.DockerCleanupFrequency,
            server.DockerCleanupForce,
            server.DeleteUnusedVolumes,
            server.DeleteUnusedNetworks,
            server.DisableAppImageRetention,
        });
    }

    /// <summary>Save Docker cleanup configuration.</summary>
    [HttpPut("{id:guid}/docker-cleanup")]
    public async Task<IActionResult> UpdateDockerCleanup(
        Guid id, [FromBody] UpdateDockerCleanupRequest req, CancellationToken ct)
    {
        var server = await _db.Servers.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (server is null) return NotFound();

        server.UpdateDockerCleanup(
            req.Frequency, req.Force, req.DeleteUnusedVolumes,
            req.DeleteUnusedNetworks, req.DisableAppImageRetention);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Trigger an immediate Docker cleanup on the server.</summary>
    [HttpPost("{id:guid}/docker-cleanup/run")]
    public async Task<IActionResult> TriggerDockerCleanup(Guid id, CancellationToken ct)
    {
        var server = await _db.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (server is null) return NotFound();

        var sshKey = server.SshKeyId.HasValue ? await _uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct) : null;
        if (sshKey is null) return BadRequest(new { error = "No SSH key configured on server." });

        var privateKey = _enc.Decrypt(sshKey.PrivateKeyEncrypted);

        var forceFlag   = server.DockerCleanupForce ? "--force" : "";
        var volumeFlag  = server.DeleteUnusedVolumes ? "--volumes" : "";
        var script = $"docker system prune {forceFlag} {volumeFlag} 2>&1\n" +
                     (server.DeleteUnusedNetworks ? "docker network prune --force 2>&1\n" : "") +
                     "echo 'Docker cleanup complete.'";

        var result = await _ssh.ExecuteCommandAsync(
            server.IpAddress, server.SshPort, server.SshUser, privateKey, script, ct);

        return Ok(new { success = result.Success, output = result.StdOut + result.StdErr });
    }

    // ── Cloudflare Tunnel ─────────────────────────────────────────────────────

    /// <summary>Get Cloudflare Tunnel configuration (token masked).</summary>
    [HttpGet("{id:guid}/cloudflare-tunnel")]
    public async Task<IActionResult> GetCloudflareTunnel(Guid id, CancellationToken ct)
    {
        var server = await _db.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (server is null) return NotFound();
        return Ok(new
        {
            HasToken     = !string.IsNullOrEmpty(server.CloudflareTunnelToken),
            server.CloudflareSshDomain,
            server.CloudflareTunnelManual,
        });
    }

    /// <summary>Save Cloudflare Tunnel settings and optionally install cloudflared on the server.</summary>
    [HttpPut("{id:guid}/cloudflare-tunnel")]
    public async Task<IActionResult> UpdateCloudflareTunnel(
        Guid id, [FromBody] UpdateCloudflareTunnelRequest req, CancellationToken ct)
    {
        var server = await _db.Servers.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (server is null) return NotFound();

        server.ConfigureCloudflareTunnel(req.Token, req.SshDomain, req.Manual);
        await _db.SaveChangesAsync(ct);

        // Auto-install cloudflared and apply tunnel if token + domain provided
        if (!req.Manual && !string.IsNullOrEmpty(req.Token) && !string.IsNullOrEmpty(req.SshDomain))
        {
            var sshKey = server.SshKeyId.HasValue ? await _uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct) : null;
            if (sshKey is not null)
            {
                var pk = _enc.Decrypt(sshKey.PrivateKeyEncrypted);
                var installScript =
                    "curl -fsSL https://pkg.cloudflare.com/cloudflare-main.gpg | sudo tee /usr/share/keyrings/cloudflare-main.gpg >/dev/null\n" +
                    "echo 'deb [signed-by=/usr/share/keyrings/cloudflare-main.gpg] https://pkg.cloudflare.com/cloudflared any main' | sudo tee /etc/apt/sources.list.d/cloudflared.list\n" +
                    "sudo apt-get update && sudo apt-get install -y cloudflared\n" +
                    $"cloudflared tunnel login --token {req.Token}\n" +
                    $"echo 'Tunnel configured for {req.SshDomain}'";

                await _ssh.ExecuteCommandAsync(server.IpAddress, server.SshPort, server.SshUser, pk, installScript, ct);
            }
        }

        return NoContent();
    }
}

public record ExecRequest(string Command);
public record UpdateDockerCleanupRequest(
    string? Frequency,
    bool? Force,
    bool? DeleteUnusedVolumes,
    bool? DeleteUnusedNetworks,
    bool? DisableAppImageRetention);
public record UpdateCloudflareTunnelRequest(string? Token, string? SshDomain, bool Manual = false);
