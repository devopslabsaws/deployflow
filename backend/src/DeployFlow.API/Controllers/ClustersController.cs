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
        var role = _currentUser.Role.ToLowerInvariant();
        if (role != "admin" && role != "owner")
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
        { var r = _currentUser.Role.ToLowerInvariant(); if (r != "admin" && r != "owner") return Forbid(); }

        await _clusters.AddNodeAsync(id, serverId, ct);
        return NoContent();
    }

    /// <summary>Remove a server from a cluster. Admin only.</summary>
    [HttpDelete("{id:guid}/nodes/{serverId:guid}")]
    public async Task<IActionResult> RemoveNode(Guid id, Guid serverId, CancellationToken ct)
    {
        { var r = _currentUser.Role.ToLowerInvariant(); if (r != "admin" && r != "owner") return Forbid(); }

        await _clusters.RemoveNodeAsync(id, serverId, ct);
        return NoContent();
    }

    // ── Node maintenance ──────────────────────────────────────────────────────

    /// <summary>Cordon a node: prevent new deployments from being scheduled to it.</summary>
    [HttpPost("{id:guid}/nodes/{serverId:guid}/cordon")]
    public async Task<IActionResult> CordonNode(Guid id, Guid serverId, CancellationToken ct)
    {
        { var r = _currentUser.Role.ToLowerInvariant(); if (r != "admin" && r != "owner") return Forbid(); }
        try
        {
            await _clusters.CordonNodeAsync(id, serverId, ct);
            return Ok(new { message = "Node cordoned." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Uncordon a node: allow new deployments again.</summary>
    [HttpPost("{id:guid}/nodes/{serverId:guid}/uncordon")]
    public async Task<IActionResult> UncordonNode(Guid id, Guid serverId, CancellationToken ct)
    {
        { var r = _currentUser.Role.ToLowerInvariant(); if (r != "admin" && r != "owner") return Forbid(); }
        try
        {
            await _clusters.UncordonNodeAsync(id, serverId, ct);
            return Ok(new { message = "Node uncordoned." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Drain a node: cordon it and initiate workload migration.</summary>
    [HttpPost("{id:guid}/nodes/{serverId:guid}/drain")]
    public async Task<IActionResult> DrainNode(Guid id, Guid serverId, CancellationToken ct)
    {
        { var r = _currentUser.Role.ToLowerInvariant(); if (r != "admin" && r != "owner") return Forbid(); }
        try
        {
            await _clusters.DrainNodeAsync(id, serverId, ct);
            return Ok(new { message = "Node drain initiated." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Rebalance a cluster: reset routing state and re-evaluate healthy nodes.</summary>
    [HttpPost("{id:guid}/rebalance")]
    public async Task<IActionResult> Rebalance(Guid id, CancellationToken ct)
    {
        { var r = _currentUser.Role.ToLowerInvariant(); if (r != "admin" && r != "owner") return Forbid(); }
        try
        {
            var result = await _clusters.RebalanceAsync(id, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}

public record CreateClusterRequest(string Name, string? Description, string Strategy = "RoundRobin");
