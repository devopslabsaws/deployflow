using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

/// <summary>
/// Manages multi-node clusters. Admin-only for write operations;
/// any authenticated tenant member can read cluster info.
/// </summary>
[Authorize]
[Route("api/clusters")]
public class ClustersController : BaseController
{
    private readonly ClusterService _clusters;
    private readonly ICurrentUser _currentUser;

    public ClustersController(IMediator mediator, ClusterService clusters, ICurrentUser currentUser)
        : base(mediator)
    {
        _clusters = clusters;
        _currentUser = currentUser;
    }

    /// <summary>List all clusters for the current tenant.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var clusters = await _clusters.GetForTenantAsync(_currentUser.TenantId, ct);
        return Ok(clusters.Select(c => new
        {
            c.Id, c.Name, c.Description, Strategy = c.Strategy.ToString(),
            c.IsActive, NodeCount = c.NodeIds.Count, c.NodeIds,
            c.CreatedAt, c.UpdatedAt,
        }));
    }

    /// <summary>Get the best server to deploy to for a cluster (for display/preview).</summary>
    [HttpGet("{id:guid}/next-node")]
    public async Task<IActionResult> NextNode(Guid id, CancellationToken ct)
    {
        var server = await _clusters.SelectNodeAsync(id, ct);
        if (server is null) return NotFound(new { error = "No healthy nodes in cluster." });
        return Ok(new { server.Id, server.Name, server.IpAddress, server.CpuUsagePercent });
    }

    /// <summary>Create a new cluster. Admin only.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClusterRequest req, CancellationToken ct)
    {
        if (!_currentUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        if (!Enum.TryParse<ClusterStrategy>(req.Strategy, true, out var strategy))
            return BadRequest(new { error = "Invalid strategy. Use RoundRobin, LeastLoaded, or Replicated." });

        var cluster = await _clusters.CreateAsync(
            _currentUser.TenantId, req.Name, req.Description, strategy, ct);

        return CreatedAtAction(nameof(GetAll), new { }, new
        {
            cluster.Id, cluster.Name, cluster.Description,
            Strategy = cluster.Strategy.ToString(), cluster.IsActive,
        });
    }

    /// <summary>Add a server to a cluster. Admin only.</summary>
    [HttpPost("{id:guid}/nodes/{serverId:guid}")]
    public async Task<IActionResult> AddNode(Guid id, Guid serverId, CancellationToken ct)
    {
        if (!_currentUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        await _clusters.AddNodeAsync(id, serverId, ct);
        return NoContent();
    }

    /// <summary>Remove a server from a cluster. Admin only.</summary>
    [HttpDelete("{id:guid}/nodes/{serverId:guid}")]
    public async Task<IActionResult> RemoveNode(Guid id, Guid serverId, CancellationToken ct)
    {
        if (!_currentUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        await _clusters.RemoveNodeAsync(id, serverId, ct);
        return NoContent();
    }
}

public record CreateClusterRequest(string Name, string? Description, string Strategy = "RoundRobin");
