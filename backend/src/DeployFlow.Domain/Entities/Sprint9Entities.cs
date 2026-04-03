using DeployFlow.Domain.Common;

namespace DeployFlow.Domain.Entities;

// ─── Application Template (one-click deploy gallery) ─────────────────────────

public class AppTemplate : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;           // "wordpress", "ghost", etc.
    public string Description { get; set; } = default!;
    public string Category { get; set; } = default!;       // "cms", "database", "analytics", etc.
    public string DockerImage { get; set; } = default!;    // "wordpress:latest"
    public string? ComposeYaml { get; set; }               // optional full compose spec
    public string? LogoUrl { get; set; }
    public string? DocumentationUrl { get; set; }
    public string? GithubUrl { get; set; }
    public ServiceType ServiceType { get; set; } = ServiceType.Web;
    public int DefaultPort { get; set; } = 80;
    public bool RequiresDatabase { get; set; }
    public string? DefaultDatabaseType { get; set; }       // "PostgreSQL", "MySQL", etc.
    public List<TemplateEnvVar> EnvVariables { get; set; } = new();
    public int DeployCount { get; set; }                    // popularity counter
    public bool IsOfficial { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TemplateEnvVar
{
    public string Key { get; set; } = default!;
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    public bool Required { get; set; }
    public bool IsSecret { get; set; }
}

// ─── Docker Compose Stack ─────────────────────────────────────────────────────

public class ComposeStack : TenantEntity
{
    public string Name { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? ServerId { get; set; }
    public string ComposeYaml { get; set; } = default!;
    public ComposeStackStatus Status { get; set; } = ComposeStackStatus.Stopped;
    public int ServiceCount { get; set; }
    public DateTime? LastDeployedAt { get; set; }
    public string? LastError { get; set; }
    public string? EnvironmentName { get; set; }  // dev | staging | production
}

public enum ComposeStackStatus { Stopped, Starting, Running, Failed, Removing }

// ─── Traefik Router (per-service routing rule) ─────────────────────────────────

public class TraefikRouter : TenantEntity
{
    public string Name { get; set; } = default!;            // unique router name
    public string Rule { get; set; } = default!;            // "Host(`app.example.com`)"
    public string ServiceName { get; set; } = default!;     // Traefik backend service name
    public string? Entrypoints { get; set; } = "websecure"; // "web,websecure"
    public bool TlsEnabled { get; set; } = true;
    public string? CertResolver { get; set; } = "letsencrypt";
    public int Priority { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public Guid? ServerId { get; set; }
    public Guid? DomainId { get; set; }
}

// ─── Server Provisioning Job (Terraform / cloud-init) ────────────────────────

public class ProvisioningJob : TenantEntity
{
    public string Name { get; set; } = default!;
    public string Provider { get; set; } = default!;        // "aws", "gcp", "azure", "hetzner", "digitalocean"
    public string Region { get; set; } = default!;          // "us-east-1", "fra1", etc.
    public string Size { get; set; } = default!;            // "t3.micro", "cx21", etc.
    public string Os { get; set; } = "ubuntu-22.04";
    public int SshKeyId_External { get; set; }
    public Guid? SshKeyId { get; set; }
    public ProvisioningStatus Status { get; set; } = ProvisioningStatus.Pending;
    public string? ProviderServerId { get; set; }           // cloud provider resource ID
    public string? AssignedIpAddress { get; set; }
    public string? PlanOutput { get; set; }                 // Terraform plan JSON
    public string? ErrorMessage { get; set; }
    public Guid? CreatedServerId { get; set; }              // FK once server is created
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

public enum ProvisioningStatus { Pending, Planning, Applying, Completed, Failed, Destroyed }

// ─── Auto-Recovery Rule ────────────────────────────────────────────────────────

public class RecoveryRule : TenantEntity
{
    public string Name { get; set; } = default!;
    public RecoveryTrigger Trigger { get; set; }
    public RecoveryAction Action { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int CooldownSeconds { get; set; } = 60;
    public bool IsEnabled { get; set; } = true;
    public int RetryCount { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public Guid? TargetServerId { get; set; }
    public Guid? TargetProjectId { get; set; }
}

public enum RecoveryTrigger { ContainerCrash, ServerOffline, DeploymentFailed, HealthCheckFailed }
public enum RecoveryAction { RestartContainer, RedeployLastGood, AlertOnly, ScaleUp }
