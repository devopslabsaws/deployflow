using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeployFlow.Infrastructure.Services;

/// <summary>
/// Manages multi-node cluster state and selects the best server for a deployment
/// based on the cluster's configured strategy.
/// </summary>
public class ClusterService
{
    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ClusterService> _logger;

    // Round-robin cursor per cluster (in-memory; resets on restart — acceptable for this use case)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int> _rrCursors = new();

    public ClusterService(ApplicationDbContext db, IUnitOfWork uow, ILogger<ClusterService> logger)
    {
        _db = db;
        _uow = uow;
        _logger = logger;
    }

    /// <summary>
    /// Selects the best server in the cluster for the next deployment.
    /// Returns null if the cluster has no healthy nodes.
    /// </summary>
    public async Task<Server?> SelectNodeAsync(Guid clusterId, CancellationToken ct = default)
    {
        var cluster = await _db.Clusters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clusterId && c.IsActive && !c.IsDeleted, ct);

        if (cluster is null || cluster.NodeIds.Count == 0)
        {
            _logger.LogWarning("Cluster {Id} has no nodes or is inactive.", clusterId);
            return null;
        }

        var onlineServers = await _uow.Servers.GetOnlineServersAsync(cluster.TenantId, ct);
        var candidates = onlineServers
            .Where(s => cluster.NodeIds.Contains(s.Id))
            .ToList();

        if (candidates.Count == 0)
        {
            _logger.LogWarning("Cluster {Id}: all {Count} nodes are offline.", clusterId, cluster.NodeIds.Count);
            return null;
        }

        return cluster.Strategy switch
        {
            ClusterStrategy.LeastLoaded => candidates.MinBy(s => s.CpuUsagePercent),
            ClusterStrategy.RoundRobin  => RoundRobinSelect(clusterId, candidates),
            ClusterStrategy.Replicated  => candidates.First(), // caller handles fanout
            _                           => candidates.First(),
        };
    }

    /// <summary>
    /// For Replicated strategy: returns ALL online nodes in the cluster.
    /// </summary>
    public async Task<IReadOnlyList<Server>> GetAllNodesAsync(Guid clusterId, CancellationToken ct = default)
    {
        var cluster = await _db.Clusters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clusterId && c.IsActive && !c.IsDeleted, ct);

        if (cluster is null) return [];

        var onlineServers = await _uow.Servers.GetOnlineServersAsync(cluster.TenantId, ct);
        return onlineServers.Where(s => cluster.NodeIds.Contains(s.Id)).ToList();
    }

    public async Task<Cluster> CreateAsync(
        Guid tenantId, string name, string? description, ClusterStrategy strategy, CancellationToken ct)
    {
        var cluster = Cluster.Create(tenantId, name, description, strategy);
        _db.Clusters.Add(cluster);
        await _db.SaveChangesAsync(ct);
        return cluster;
    }

    public async Task AddNodeAsync(Guid clusterId, Guid serverId, CancellationToken ct)
    {
        var cluster = await _db.Clusters.FindAsync([clusterId], ct)
            ?? throw new KeyNotFoundException($"Cluster {clusterId} not found.");
        cluster.AddNode(serverId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveNodeAsync(Guid clusterId, Guid serverId, CancellationToken ct)
    {
        var cluster = await _db.Clusters.FindAsync([clusterId], ct)
            ?? throw new KeyNotFoundException($"Cluster {clusterId} not found.");
        cluster.RemoveNode(serverId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<Cluster>> GetForTenantAsync(Guid tenantId, CancellationToken ct) =>
        await _db.Clusters
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .ToListAsync(ct);

    // ── Cordon / Uncordon / Drain ──────────────────────────────────────────────

    /// <summary>Marks a server node as cordoned: no new deployments routed to it.</summary>
    public async Task CordonNodeAsync(Guid clusterId, Guid serverId, CancellationToken ct)
    {
        var cluster = await _db.Clusters.FindAsync([clusterId], ct)
            ?? throw new KeyNotFoundException($"Cluster {clusterId} not found.");
        if (!cluster.NodeIds.Contains(serverId))
            throw new InvalidOperationException($"Server {serverId} is not a member of cluster {clusterId}.");

        var server = await _db.Servers.FindAsync([serverId], ct)
            ?? throw new KeyNotFoundException($"Server {serverId} not found.");
        server.Cordon();
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Removes cordon from a server node, allowing new deployments.</summary>
    public async Task UncordonNodeAsync(Guid clusterId, Guid serverId, CancellationToken ct)
    {
        var cluster = await _db.Clusters.FindAsync([clusterId], ct)
            ?? throw new KeyNotFoundException($"Cluster {clusterId} not found.");
        if (!cluster.NodeIds.Contains(serverId))
            throw new InvalidOperationException($"Server {serverId} is not a member of cluster {clusterId}.");

        var server = await _db.Servers.FindAsync([serverId], ct)
            ?? throw new KeyNotFoundException($"Server {serverId} not found.");
        server.Uncordon();
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Drains a node: cordon it and flag it for workload migration.</summary>
    public async Task DrainNodeAsync(Guid clusterId, Guid serverId, CancellationToken ct)
    {
        var cluster = await _db.Clusters.FindAsync([clusterId], ct)
            ?? throw new KeyNotFoundException($"Cluster {clusterId} not found.");
        if (!cluster.NodeIds.Contains(serverId))
            throw new InvalidOperationException($"Server {serverId} is not a member of cluster {clusterId}.");

        var server = await _db.Servers.FindAsync([serverId], ct)
            ?? throw new KeyNotFoundException($"Server {serverId} not found.");
        server.Drain();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Node {ServerId} in cluster {ClusterId} is now draining.", serverId, clusterId);
    }

    /// <summary>
    /// Rebalances a cluster by redistributing the round-robin cursor and
    /// clearing per-node load state so the next selection starts fresh.
    /// </summary>
    public async Task<RebalanceResult> RebalanceAsync(Guid clusterId, CancellationToken ct)
    {
        var cluster = await _db.Clusters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clusterId && !c.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Cluster {clusterId} not found.");

        // Reset round-robin cursor
        _rrCursors.TryRemove(clusterId, out _);

        var nodeIds = cluster.NodeIds;
        var servers = await _db.Servers
            .Where(s => nodeIds.Contains(s.Id) && !s.IsDeleted)
            .Select(s => new { s.Id, s.Name, s.Status, s.IsCordoned, s.IsDraining, s.CpuUsagePercent })
            .ToListAsync(ct);

        var healthy = servers.Count(s => s.Status == ServerStatus.Online && !s.IsCordoned && !s.IsDraining);
        _logger.LogInformation(
            "Cluster {Id} rebalanced: {Healthy}/{Total} nodes healthy.", clusterId, healthy, servers.Count);

        return new RebalanceResult(clusterId, servers.Count, healthy);
    }

    private static Server RoundRobinSelect(Guid clusterId, List<Server> candidates)
    {
        var idx = _rrCursors.AddOrUpdate(clusterId, 0, (_, prev) => (prev + 1) % candidates.Count);
        return candidates[idx % candidates.Count];
    }
}

public record RebalanceResult(Guid ClusterId, int TotalNodes, int HealthyNodes);
