namespace DeployFlow.Application.Services;

/// <summary>
/// Adapter pattern for deployment providers.
/// Implementations push a built artifact to a target platform.
///
/// Current providers: Docker-SSH (existing), Vercel, Netlify, AWS S3/EC2/Lambda stubs.
/// </summary>
public interface IDeploymentProvider
{
    string ProviderName { get; }
    bool   Supports(string providerKey);

    Task<DeploymentProviderResult> DeployAsync(
        DeploymentProviderContext context,
        CancellationToken         ct = default);

    Task<string> GetDeploymentUrlAsync(
        DeploymentProviderContext context,
        CancellationToken         ct = default);
}

// ── Context / Result ──────────────────────────────────────────────────────────

public sealed class DeploymentProviderContext
{
    public required string   ProjectName    { get; init; }
    public required string   Environment    { get; init; }  // dev | staging | prod
    public string?           ArtifactPath   { get; init; }  // local path or image tag
    public string?           ImageTag       { get; init; }
    public string?           Branch         { get; init; }
    public string?           CommitSha      { get; init; }
    public Dictionary<string, string> ProviderConfig { get; init; } = [];
    public Dictionary<string, string> EnvVars        { get; init; } = [];
}

public sealed class DeploymentProviderResult
{
    public bool    Succeeded    { get; init; }
    public string? DeployUrl    { get; init; }
    public string? Message      { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}

// ── Vercel Provider ──────────────────────────────────────────────────────────

/// <summary>
/// Deploys to Vercel via the Vercel Deploy Hook or CLI.
/// Config keys: vercel_token, vercel_project_id, vercel_org_id.
/// </summary>
public sealed class VercelDeploymentProvider : IDeploymentProvider
{
    private readonly HttpClient _http;

    public VercelDeploymentProvider(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("vercel");
        _http.BaseAddress = new Uri("https://api.vercel.com");
    }

    public string ProviderName => "Vercel";
    public bool   Supports(string key) => key.Equals("vercel", StringComparison.OrdinalIgnoreCase);

    public async Task<DeploymentProviderResult> DeployAsync(
        DeploymentProviderContext context,
        CancellationToken         ct = default)
    {
        context.ProviderConfig.TryGetValue("vercel_token",      out var token);
        context.ProviderConfig.TryGetValue("vercel_project_id", out var projectId);
        context.ProviderConfig.TryGetValue("vercel_org_id",     out var orgId);
        context.ProviderConfig.TryGetValue("deploy_hook",       out var deployHook);

        // Prefer deploy hook (simpler, no token required in payload)
        if (!string.IsNullOrWhiteSpace(deployHook))
        {
            using var hookResp = await _http.PostAsync(deployHook, null, ct);
            if (!hookResp.IsSuccessStatusCode)
                return Fail($"Deploy hook returned HTTP {(int)hookResp.StatusCode}");

            var body = await hookResp.Content.ReadAsStringAsync(ct);
            return new DeploymentProviderResult
            {
                Succeeded = true,
                Message   = $"Vercel deploy hook triggered. Response: {body[..Math.Min(200, body.Length)]}",
            };
        }

        // Use Vercel REST API (v13 deployments)
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(projectId))
            return Fail("Vercel: vercel_token and vercel_project_id are required (or use deploy_hook)");

        _http.DefaultRequestHeaders.Remove("Authorization");
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            name      = projectId,
            gitSource = new
            {
                type      = "github",
                @ref      = context.Branch ?? "main",
                sha       = context.CommitSha,
            },
            target    = context.Environment == "prod" ? "production" : context.Environment,
        });

        using var req  = new HttpRequestMessage(HttpMethod.Post, $"/v13/deployments?teamId={orgId}")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
        };
        using var resp = await _http.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            return Fail($"Vercel API {(int)resp.StatusCode}: {err[..Math.Min(300, err.Length)]}");
        }

        var json = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        string? deployId = json.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        string? url      = json.RootElement.TryGetProperty("url", out var u)  ? $"https://{u.GetString()}" : null;

        return new DeploymentProviderResult
        {
            Succeeded = true,
            DeployUrl = url,
            Message   = $"Vercel deployment {deployId} created.",
            Metadata  = { ["deployment_id"] = deployId ?? "" },
        };
    }

    public async Task<string> GetDeploymentUrlAsync(DeploymentProviderContext context, CancellationToken ct = default)
    {
        context.ProviderConfig.TryGetValue("vercel_project_id", out var proj);
        return $"https://{proj ?? context.ProjectName.ToLowerInvariant()}.vercel.app";
    }

    private static DeploymentProviderResult Fail(string msg) =>
        new() { Succeeded = false, ErrorMessage = msg };
}

// ── Netlify Provider ─────────────────────────────────────────────────────────

/// <summary>
/// Deploys a static build directory to Netlify.
/// Config keys: netlify_token, netlify_site_id.
/// </summary>
public sealed class NetlifyDeploymentProvider : IDeploymentProvider
{
    private readonly HttpClient _http;

    public NetlifyDeploymentProvider(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("netlify");
        _http.BaseAddress = new Uri("https://api.netlify.com");
    }

    public string ProviderName => "Netlify";
    public bool   Supports(string key) => key.Equals("netlify", StringComparison.OrdinalIgnoreCase);

    public async Task<DeploymentProviderResult> DeployAsync(
        DeploymentProviderContext context,
        CancellationToken         ct = default)
    {
        context.ProviderConfig.TryGetValue("netlify_token",   out var token);
        context.ProviderConfig.TryGetValue("netlify_site_id", out var siteId);

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(siteId))
            return Fail("Netlify: netlify_token and netlify_site_id required");

        _http.DefaultRequestHeaders.Remove("Authorization");
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        // Trigger a build hook
        context.ProviderConfig.TryGetValue("build_hook", out var buildHook);
        if (!string.IsNullOrWhiteSpace(buildHook))
        {
            using var hookResp = await _http.PostAsync(buildHook, null, ct);
            return hookResp.IsSuccessStatusCode
                ? new DeploymentProviderResult { Succeeded = true, Message = "Netlify build hook triggered." }
                : Fail($"Netlify hook returned HTTP {(int)hookResp.StatusCode}");
        }

        // Trigger deploy via API for the site
        using var req  = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/sites/{siteId}/deploys");
        req.Content    = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            return Fail($"Netlify API {(int)resp.StatusCode}: {err[..Math.Min(300, err.Length)]}");
        }

        var json = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        string? deployId = json.RootElement.TryGetProperty("id",  out var id) ? id.GetString() : null;
        string? url      = json.RootElement.TryGetProperty("ssl_url", out var u) ? u.GetString() : null;

        return new DeploymentProviderResult
        {
            Succeeded = true,
            DeployUrl = url,
            Message   = $"Netlify deploy {deployId} created.",
        };
    }

    public Task<string> GetDeploymentUrlAsync(DeploymentProviderContext context, CancellationToken ct = default)
    {
        context.ProviderConfig.TryGetValue("netlify_site_id", out var siteId);
        return Task.FromResult($"https://{siteId ?? context.ProjectName}.netlify.app");
    }

    private static DeploymentProviderResult Fail(string msg) =>
        new() { Succeeded = false, ErrorMessage = msg };
}

// ── AWS S3 Static Provider ───────────────────────────────────────────────────

/// <summary>
/// Deploys a static build to AWS S3 + CloudFront invalidation.
/// Uses AWS CLI commands executed on the runner host.
/// Config keys: aws_access_key_id, aws_secret_access_key, aws_region, s3_bucket, cf_distribution_id.
/// </summary>
public sealed class AwsS3DeploymentProvider : IDeploymentProvider
{
    public string ProviderName => "AWS S3";
    public bool   Supports(string key) => key.Equals("aws-s3", StringComparison.OrdinalIgnoreCase)
                                       || key.Equals("aws_s3", StringComparison.OrdinalIgnoreCase);

    public async Task<DeploymentProviderResult> DeployAsync(
        DeploymentProviderContext context,
        CancellationToken         ct = default)
    {
        context.ProviderConfig.TryGetValue("s3_bucket",              out var bucket);
        context.ProviderConfig.TryGetValue("aws_region",             out var region);
        context.ProviderConfig.TryGetValue("cf_distribution_id",     out var cfId);
        context.ProviderConfig.TryGetValue("build_dir",              out var buildDir);

        buildDir ??= "./dist";
        region   ??= "us-east-1";

        if (string.IsNullOrWhiteSpace(bucket))
            return Fail("AWS S3: s3_bucket config key required");

        // These commands are designed to be run via SSH / shell step; we return them
        // for PipelineRunnerService to execute. This provider generates the command set.
        var commands = new List<string>
        {
            $"aws s3 sync {buildDir} s3://{bucket} --delete --region {region}",
        };

        if (!string.IsNullOrWhiteSpace(cfId))
            commands.Add($"aws cloudfront create-invalidation --distribution-id {cfId} --paths '/*'");

        return new DeploymentProviderResult
        {
            Succeeded = true,
            DeployUrl = $"https://{bucket}.s3-website-{region}.amazonaws.com",
            Message   = "AWS S3 deploy commands generated:\n" + string.Join("\n", commands),
            Metadata  = { ["commands"] = string.Join(";", commands) },
        };
    }

    public Task<string> GetDeploymentUrlAsync(DeploymentProviderContext context, CancellationToken ct = default)
    {
        context.ProviderConfig.TryGetValue("s3_bucket",  out var bucket);
        context.ProviderConfig.TryGetValue("aws_region", out var region);
        return Task.FromResult($"https://{bucket}.s3-website-{region ?? "us-east-1"}.amazonaws.com");
    }

    private static DeploymentProviderResult Fail(string msg) =>
        new() { Succeeded = false, ErrorMessage = msg };
}

// ── Registry / Factory ────────────────────────────────────────────────────────

/// <summary>Resolves the correct <see cref="IDeploymentProvider"/> by key.</summary>
public sealed class DeploymentProviderRegistry
{
    private readonly IEnumerable<IDeploymentProvider> _providers;

    public DeploymentProviderRegistry(IEnumerable<IDeploymentProvider> providers)
        => _providers = providers;

    public IDeploymentProvider? Resolve(string providerKey)
        => _providers.FirstOrDefault(p => p.Supports(providerKey));

    public IEnumerable<string> AvailableProviders()
        => _providers.Select(p => p.ProviderName);
}
