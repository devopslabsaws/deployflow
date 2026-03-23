using DeployFlow.Domain.Common;

namespace DeployFlow.Domain.Entities;

/// <summary>
/// A named cluster groups multiple servers so deployments can be spread
/// across nodes using round-robin or load-based selection.
/// </summary>
public class Cluster : TenantEntity
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public ClusterStrategy Strategy { get; private set; } = ClusterStrategy.RoundRobin;
    public bool IsActive { get; private set; } = true;

    // Ordered list of Server IDs belonging to this cluster
    private readonly List<Guid> _nodeIds = new();
    public IReadOnlyList<Guid> NodeIds => _nodeIds.AsReadOnly();

    private Cluster() { }

    public static Cluster Create(
        Guid tenantId,
        string name,
        string? description = null,
        ClusterStrategy strategy = ClusterStrategy.RoundRobin)
        => new()
        {
            TenantId = tenantId,
            Name = name,
            Description = description,
            Strategy = strategy,
        };

    public void AddNode(Guid serverId)
    {
        if (!_nodeIds.Contains(serverId))
            _nodeIds.Add(serverId);
        Touch();
    }

    public void RemoveNode(Guid serverId)
    {
        _nodeIds.Remove(serverId);
        Touch();
    }

    public void Update(string? name = null, string? description = null, ClusterStrategy? strategy = null)
    {
        if (name is not null) Name = name;
        if (description is not null) Description = description;
        if (strategy.HasValue) Strategy = strategy.Value;
        Touch();
    }

    public void Deactivate() { IsActive = false; Touch(); }
    public void Activate()   { IsActive = true;  Touch(); }
}

public enum ClusterStrategy
{
    /// <summary>Each deployment cycles to the next server in the cluster.</summary>
    RoundRobin,
    /// <summary>Always picks the server with the lowest CPU usage.</summary>
    LeastLoaded,
    /// <summary>Always deploys to all nodes simultaneously (replicated).</summary>
    Replicated,
}
