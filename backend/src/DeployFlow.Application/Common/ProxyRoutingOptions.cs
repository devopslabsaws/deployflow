namespace DeployFlow.Application.Common;

public class ProxyRoutingOptions
{
    public bool Enabled { get; set; } = false;
    public string? BaseDomain { get; set; }
    public string Scheme { get; set; } = "https";
    public string? DockerNetwork { get; set; }
    public string? EntryPoints { get; set; } = "web,websecure";
    public bool TlsEnabled { get; set; } = true;
    public string? CertResolver { get; set; } = "letsencrypt";
}