using DeployFlow.Domain.Common;

namespace DeployFlow.Domain.Entities;

public enum ServerStatus
{
    Online,
    Offline,
    Provisioning,
    Maintenance,
    Error
}

public enum ServerProvider
{
    Custom,
    Aws,
    Azure,
    Gcp,
    DigitalOcean,
    Hetzner,
    Vultr
}

public class Server : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Hostname { get; private set; } = default!;
    public string IpAddress { get; private set; } = default!;
    public int SshPort { get; private set; } = 22;
    public string SshUser { get; private set; } = "root";
    public Guid? SshKeyId { get; private set; }
    public ServerStatus Status { get; private set; } = ServerStatus.Provisioning;
    public ServerProvider Provider { get; private set; } = ServerProvider.Custom;
    public string? Region { get; private set; }
    public string? Os { get; private set; }
    public string? Arch { get; private set; }
    public int CpuCount { get; private set; }
    public int MemoryGb { get; private set; }
    public int DiskGb { get; private set; }
    public double CpuUsagePercent { get; private set; }
    public double MemoryUsagePercent { get; private set; }
    public double DiskUsagePercent { get; private set; }
    public int ActiveContainers { get; private set; }
    public bool IsSwarmManager { get; private set; }
    public bool IsSwarmWorker { get; private set; }
    public bool KubernetesEnabled { get; private set; }
    public string? DockerVersion { get; private set; }
    public List<string> Tags { get; private set; } = new();
    public DateTime? LastHealthCheckAt { get; private set; }
    public string? SwarmNodeId { get; private set; }

    // Navigation
    public ICollection<Project> Projects { get; private set; } = new List<Project>();

    private Server() { }

    public static Server Create(
        Guid tenantId,
        string name,
        string ipAddress,
        int sshPort = 22,
        string sshUser = "root",
        Guid? sshKeyId = null,
        ServerProvider provider = ServerProvider.Custom,
        string? region = null,
        int cpuCount = 1,
        int memoryGb = 2,
        int diskGb = 20)
    {
        return new Server
        {
            TenantId = tenantId,
            Name = name,
            Hostname = ipAddress,
            IpAddress = ipAddress,
            SshPort = sshPort,
            SshUser = sshUser,
            SshKeyId = sshKeyId,
            Provider = provider,
            Region = region,
            CpuCount = cpuCount,
            MemoryGb = memoryGb,
            DiskGb = diskGb,
        };
    }

    public void UpdateMetrics(double cpu, double memory, double disk, int containers)
    {
        CpuUsagePercent = cpu;
        MemoryUsagePercent = memory;
        DiskUsagePercent = disk;
        ActiveContainers = containers;
        Touch();
    }

    public void UpdateSpecs(int cpuCores, int memoryGb, int diskGb)
    {
        if (cpuCores > 0) CpuCount = cpuCores;
        if (memoryGb > 0) MemoryGb = memoryGb;
        if (diskGb > 0) DiskGb = diskGb;
        Touch();
    }

    public void SetOnline(string? dockerVersion = null, string? os = null, string? region = null)
    {
        Status = ServerStatus.Online;
        if (dockerVersion is not null) DockerVersion = dockerVersion;
        if (os is not null) Os = os;
        if (region is not null && Region is null) Region = region;
        LastHealthCheckAt = DateTime.UtcNow;
        Touch();
    }

    public void SetOffline()
    {
        Status = ServerStatus.Offline;
        LastHealthCheckAt = DateTime.UtcNow;
        Touch();
    }

    public void SetMaintenance() { Status = ServerStatus.Maintenance; Touch(); }

    public void UpdateDetails(string? name, Guid? sshKeyId, int? sshPort, string? sshUser, string? region)
    {
        if (name is not null) Name = name;
        if (sshKeyId.HasValue) SshKeyId = sshKeyId;
        if (sshPort.HasValue) SshPort = sshPort.Value;
        if (sshUser is not null) SshUser = sshUser;
        if (region is not null) Region = region;
        Touch();
    }

    public void RecordHealthCheck()
    {
        LastHealthCheckAt = DateTime.UtcNow;
        Touch();
    }

    public void ConfigureSwarm(bool isManager, bool isWorker, string? nodeId = null)
    {
        IsSwarmManager = isManager;
        IsSwarmWorker = isWorker;
        SwarmNodeId = nodeId;
        Touch();
    }

    public void EnableKubernetes()
    {
        KubernetesEnabled = true;
        Touch();
    }

    // ── Docker Cleanup configuration ─────────────────────────────────────────

    /// <summary>Cron expression for automatic Docker cleanup (e.g. "0 0 * * *").</summary>
    public string DockerCleanupFrequency { get; private set; } = "0 0 * * *";

    /// <summary>docker system prune --force (removes stopped containers, dangling images, unused networks).</summary>
    public bool DockerCleanupForce { get; private set; } = true;

    /// <summary>Also remove unused volumes (docker volume prune). Use with caution.</summary>
    public bool DeleteUnusedVolumes { get; private set; } = false;

    /// <summary>Also remove unused networks.</summary>
    public bool DeleteUnusedNetworks { get; private set; } = false;

    /// <summary>Disable retaining old application images (removes all previous deployments' images).</summary>
    public bool DisableAppImageRetention { get; private set; } = false;

    public void UpdateDockerCleanup(
        string? frequency = null,
        bool? force = null,
        bool? deleteVolumes = null,
        bool? deleteNetworks = null,
        bool? disableRetention = null)
    {
        if (frequency is not null) DockerCleanupFrequency = frequency;
        if (force.HasValue) DockerCleanupForce = force.Value;
        if (deleteVolumes.HasValue) DeleteUnusedVolumes = deleteVolumes.Value;
        if (deleteNetworks.HasValue) DeleteUnusedNetworks = deleteNetworks.Value;
        if (disableRetention.HasValue) DisableAppImageRetention = disableRetention.Value;
        Touch();
    }

    // ── Cloudflare Tunnel configuration ──────────────────────────────────────

    /// <summary>Cloudflare Tunnel API token (encrypted at rest).</summary>
    public string? CloudflareTunnelToken { get; private set; }

    /// <summary>Configured SSH domain exposed through the Cloudflare Tunnel.</summary>
    public string? CloudflareSshDomain { get; private set; }

    /// <summary>Whether the tunnel was configured manually (vs. automated).</summary>
    public bool CloudflareTunnelManual { get; private set; } = false;

    public void ConfigureCloudflareTunnel(string? token, string? sshDomain, bool manual = false)
    {
        CloudflareTunnelToken = token;
        CloudflareSshDomain   = sshDomain;
        CloudflareTunnelManual = manual;
        Touch();
    }
}
