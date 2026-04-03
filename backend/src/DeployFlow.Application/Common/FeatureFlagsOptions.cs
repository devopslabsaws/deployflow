namespace DeployFlow.Application.Common;

public class FeatureFlagsOptions
{
    public bool MultiEnvironmentEnabled { get; set; } = true;
    public bool AutoPipelineEnabled { get; set; } = true;
    public bool AutoRollbackEnabled { get; set; } = true;
    public bool ImmutableDeploymentsOnlyEnabled { get; set; } = false;
    public bool ImmutableSafeRolloutEnabled { get; set; } = false;
}