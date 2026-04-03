namespace DeployFlow.Domain.Entities;

/// <summary>
/// Stores a point-in-time snapshot of a deployed pipeline state for rollback support.
/// </summary>
public sealed class PipelineSnapshot
{
    public Guid      Id            { get; set; } = Guid.NewGuid();
    public Guid      PipelineId    { get; set; }
    public string    PipelineName  { get; set; } = "";
    public string?   ImageTag      { get; set; }
    public string?   ContainerName { get; set; }
    public string    ConfigJson    { get; set; } = "{}";
    public bool      IsHealthy     { get; set; }
    public DateTime  CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime? HealthedAt    { get; set; }
    public DateTime? RolledBackAt  { get; set; }
}
