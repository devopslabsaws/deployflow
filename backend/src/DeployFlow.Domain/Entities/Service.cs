using DeployFlow.Domain.Common;

namespace DeployFlow.Domain.Entities;

public enum ServiceStatus { Running, Stopped, Starting, Restarting, Error }
public enum ServiceType { Web, Api, Worker, Cron, Database, Cache, Queue, Storage }

public class Service : AggregateRoot
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    public ServiceType Type { get; private set; }
    public ServiceStatus Status { get; private set; } = ServiceStatus.Stopped;
    public string? Image { get; private set; }
    public string? Tag { get; private set; }
    public int Replicas { get; private set; } = 1;
    public Guid? DomainId { get; private set; }
    public string? ContainerId { get; private set; }
    public string? CpuLimit { get; private set; }
    public string? MemoryLimit { get; private set; }
    public string? CpuRequest { get; private set; }
    public string? MemoryRequest { get; private set; }
    public int MinReplicas { get; private set; } = 1;
    public int MaxReplicas { get; private set; } = 1;
    public int? CpuTargetPercentage { get; private set; }
    public int? MemoryTargetPercentage { get; private set; }
    public string? LastScalingAction { get; private set; }
    public string? LastScalingReason { get; private set; }
    public DateTime? LastScaledAt { get; private set; }
    public string? HealthCheckPath { get; private set; }
    public int HealthCheckInterval { get; private set; } = 30;
    public int HealthCheckTimeout { get; private set; } = 10;
    public int HealthCheckRetries { get; private set; } = 3;
    public int HealthCheckStartPeriod { get; private set; } = 10;

    // Navigation
    public Project Project { get; private set; } = default!;
    public ICollection<EnvVariable> EnvVariables { get; private set; } = new List<EnvVariable>();

    private Service() { }

    public static Service Create(Guid tenantId, Guid projectId, string name, ServiceType type, string? image = null)
    {
        return new Service
        {
            TenantId = tenantId,
            ProjectId = projectId,
            Name = name,
            Type = type,
            Image = image,
        };
    }

    public void SetStatus(ServiceStatus status) { Status = status; Touch(); }
    public void SetContainerId(string id) { ContainerId = id; Touch(); }
    public void SetReplicas(int count)
    {
        Replicas = Math.Max(0, count);
        if (Replicas < MinReplicas)
        {
            MinReplicas = Replicas;
        }
        if (Replicas > MaxReplicas)
        {
            MaxReplicas = Replicas;
        }
        Touch();
    }

    public void ConfigureResources(
        string? cpuLimit = null,
        string? memoryLimit = null,
        string? cpuRequest = null,
        string? memoryRequest = null)
    {
        CpuLimit = cpuLimit;
        MemoryLimit = memoryLimit;
        CpuRequest = cpuRequest;
        MemoryRequest = memoryRequest;
        Touch();
    }

    public void ConfigureScalingPolicy(int minReplicas, int maxReplicas, int? cpuTargetPercentage, int? memoryTargetPercentage)
    {
        MinReplicas = Math.Max(0, minReplicas);
        MaxReplicas = Math.Max(MinReplicas, maxReplicas);
        CpuTargetPercentage = cpuTargetPercentage;
        MemoryTargetPercentage = memoryTargetPercentage;

        if (Replicas < MinReplicas)
        {
            Replicas = MinReplicas;
        }
        else if (Replicas > MaxReplicas)
        {
            Replicas = MaxReplicas;
        }

        Touch();
    }

    public void RecordScalingDecision(string? action, string? reason, DateTime? scaledAt = null)
    {
        LastScalingAction = string.IsNullOrWhiteSpace(action) ? null : action;
        LastScalingReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
        LastScaledAt = scaledAt ?? DateTime.UtcNow;
        Touch();
    }
}

public class DeploymentLog : BaseEntity
{
    public Guid DeploymentId { get; set; }
    public string Message { get; set; } = default!;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string? Stream { get; set; } // "stdout" | "stderr"
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Deployment Deployment { get; set; } = default!;
}

public enum LogLevel { Debug, Info, Warn, Error }
