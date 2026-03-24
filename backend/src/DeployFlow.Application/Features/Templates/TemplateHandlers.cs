using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Templates;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record TemplateDto(
    string Id,
    string Name,
    string Slug,
    string Description,
    string Category,
    string DockerImage,
    string? LogoUrl,
    string? DocumentationUrl,
    string? GithubUrl,
    string ServiceType,
    int DefaultPort,
    bool RequiresDatabase,
    string? DefaultDatabaseType,
    List<TemplateEnvVarDto> EnvVariables,
    int DeployCount,
    bool IsOfficial,
    bool HasComposeYaml
);

public record TemplateEnvVarDto(string Key, string? DefaultValue, string? Description, bool Required, bool IsSecret);
public record DeployTemplateRequest(string Slug, Guid ProjectId, Guid? ServerId, Dictionary<string, string>? EnvOverrides, string? EnvironmentName);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetTemplatesQuery(string? Category) : IRequest<Result<List<TemplateDto>>>;

public class GetTemplatesQueryHandler : IRequestHandler<GetTemplatesQuery, Result<List<TemplateDto>>>
{
    // Curated built-in template library – no DB needed
    private static readonly List<AppTemplate> _builtIn = BuildTemplates();

    public Task<Result<List<TemplateDto>>> Handle(GetTemplatesQuery request, CancellationToken ct)
    {
        var filtered = _builtIn.AsEnumerable();
        if (!string.IsNullOrEmpty(request.Category))
            filtered = filtered.Where(t => t.Category.Equals(request.Category, StringComparison.OrdinalIgnoreCase));

        var dtos = filtered.Select(Map).ToList();
        return Task.FromResult(Result<List<TemplateDto>>.Success(dtos));
    }

    private static TemplateDto Map(AppTemplate t) => new(
        t.Slug, t.Name, t.Slug, t.Description, t.Category, t.DockerImage,
        t.LogoUrl, t.DocumentationUrl, t.GithubUrl,
        t.ServiceType.ToString().ToLower(), t.DefaultPort, t.RequiresDatabase,
        t.DefaultDatabaseType,
        t.EnvVariables.Select(e => new TemplateEnvVarDto(e.Key, e.DefaultValue, e.Description, e.Required, e.IsSecret)).ToList(),
        t.DeployCount, t.IsOfficial, t.ComposeYaml is not null);

    internal static List<AppTemplate> BuildTemplates() => new()
    {
        new() { Name = "WordPress", Slug = "wordpress", Description = "The world's most popular CMS — blog, portfolio, or full website.", Category = "cms", DockerImage = "wordpress:latest", LogoUrl = "/logos/wordpress.svg", GithubUrl = "https://github.com/WordPress/WordPress", ServiceType = ServiceType.Web, DefaultPort = 80, RequiresDatabase = true, DefaultDatabaseType = "MySQL", IsOfficial = true, DeployCount = 4200,
            EnvVariables = [
                new() { Key = "WORDPRESS_DB_HOST", DefaultValue = "db:3306", Required = true },
                new() { Key = "WORDPRESS_DB_NAME", DefaultValue = "wordpress", Required = true },
                new() { Key = "WORDPRESS_DB_USER", DefaultValue = "wp", Required = true },
                new() { Key = "WORDPRESS_DB_PASSWORD", Required = true, IsSecret = true },
            ] },
        new() { Name = "Ghost", Slug = "ghost", Description = "Professional publishing platform for blogs and newsletters.", Category = "cms", DockerImage = "ghost:5", LogoUrl = "/logos/ghost.svg", ServiceType = ServiceType.Web, DefaultPort = 2368, RequiresDatabase = true, DefaultDatabaseType = "MySQL", IsOfficial = true, DeployCount = 1800,
            EnvVariables = [
                new() { Key = "url", DefaultValue = "http://localhost:2368", Required = true },
                new() { Key = "database__client", DefaultValue = "mysql", Required = true },
            ] },
        new() { Name = "Plausible Analytics", Slug = "plausible", Description = "Privacy-friendly Google Analytics alternative.", Category = "analytics", DockerImage = "plausible/analytics:latest", LogoUrl = "/logos/plausible.svg", GithubUrl = "https://github.com/plausible/analytics", ServiceType = ServiceType.Web, DefaultPort = 8000, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 920,
            EnvVariables = [
                new() { Key = "BASE_URL", Required = true, Description = "Your public-facing URL" },
                new() { Key = "SECRET_KEY_BASE", Required = true, IsSecret = true },
            ] },
        new() { Name = "Pocketbase", Slug = "pocketbase", Description = "Open-source backend in 1 file — DB, Auth, Admin UI.", Category = "backend", DockerImage = "ghcr.io/muchobien/pocketbase:latest", LogoUrl = "/logos/pocketbase.svg", GithubUrl = "https://github.com/pocketbase/pocketbase", ServiceType = ServiceType.Api, DefaultPort = 8090, RequiresDatabase = false, IsOfficial = true, DeployCount = 1250, EnvVariables = [] },
        new() { Name = "Cal.com", Slug = "calcom", Description = "Open-source Calendly alternative for scheduling.", Category = "productivity", DockerImage = "calcom/cal.com:latest", LogoUrl = "/logos/calcom.svg", GithubUrl = "https://github.com/calcom/cal.com", ServiceType = ServiceType.Fullstack, DefaultPort = 3000, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 670,
            EnvVariables = [
                new() { Key = "DATABASE_URL", Required = true, IsSecret = true },
                new() { Key = "NEXTAUTH_SECRET", Required = true, IsSecret = true },
            ] },
        new() { Name = "n8n", Slug = "n8n", Description = "Workflow automation tool — like Zapier but self-hosted.", Category = "automation", DockerImage = "n8nio/n8n:latest", LogoUrl = "/logos/n8n.svg", GithubUrl = "https://github.com/n8n-io/n8n", ServiceType = ServiceType.Web, DefaultPort = 5678, RequiresDatabase = false, IsOfficial = true, DeployCount = 2100, EnvVariables = [
                new() { Key = "N8N_BASIC_AUTH_ACTIVE", DefaultValue = "true", Required = false },
                new() { Key = "N8N_BASIC_AUTH_USER", Required = false },
                new() { Key = "N8N_BASIC_AUTH_PASSWORD", Required = false, IsSecret = true },
            ] },
        new() { Name = "Mattermost", Slug = "mattermost", Description = "Open-source Slack alternative for team messaging.", Category = "communication", DockerImage = "mattermost/mattermost-team-edition:latest", LogoUrl = "/logos/mattermost.svg", ServiceType = ServiceType.Web, DefaultPort = 8065, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 540, EnvVariables = [
                new() { Key = "MM_SQLSETTINGS_DATASOURCE", Required = true, IsSecret = true },
            ] },
        new() { Name = "Grafana", Slug = "grafana", Description = "The open observability platform for metrics and dashboards.", Category = "monitoring", DockerImage = "grafana/grafana:latest", LogoUrl = "/logos/grafana.svg", ServiceType = ServiceType.Web, DefaultPort = 3000, RequiresDatabase = false, IsOfficial = true, DeployCount = 3100, EnvVariables = [
                new() { Key = "GF_SECURITY_ADMIN_PASSWORD", DefaultValue = "admin", Required = true, IsSecret = true },
            ] },
        new() { Name = "MinIO", Slug = "minio", Description = "S3-compatible object storage for private clouds.", Category = "storage", DockerImage = "minio/minio:latest", LogoUrl = "/logos/minio.svg", ServiceType = ServiceType.Storage, DefaultPort = 9000, RequiresDatabase = false, IsOfficial = true, DeployCount = 1640,
            EnvVariables = [
                new() { Key = "MINIO_ROOT_USER", DefaultValue = "admin", Required = true },
                new() { Key = "MINIO_ROOT_PASSWORD", Required = true, IsSecret = true },
            ] },
        new() { Name = "Nginx", Slug = "nginx", Description = "High-performance HTTP server and reverse proxy.", Category = "web", DockerImage = "nginx:alpine", ServiceType = ServiceType.Web, DefaultPort = 80, RequiresDatabase = false, IsOfficial = true, DeployCount = 5200, EnvVariables = [] },
        new() { Name = "Redis", Slug = "redis", Description = "In-memory data store for caching and pub/sub.", Category = "database", DockerImage = "redis:7-alpine", ServiceType = ServiceType.Cache, DefaultPort = 6379, RequiresDatabase = false, IsOfficial = true, DeployCount = 4800, EnvVariables = [
                new() { Key = "REDIS_PASSWORD", Required = false, IsSecret = true },
            ] },
        new() { Name = "Umami", Slug = "umami", Description = "Simple, fast, privacy-focused website analytics.", Category = "analytics", DockerImage = "ghcr.io/umami-software/umami:postgresql-latest", LogoUrl = "/logos/umami.svg", GithubUrl = "https://github.com/umami-software/umami", ServiceType = ServiceType.Web, DefaultPort = 3000, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = false, DeployCount = 410, EnvVariables = [
                new() { Key = "DATABASE_URL", Required = true, IsSecret = true },
                new() { Key = "APP_SECRET", Required = true, IsSecret = true },
            ] },
    };
}

// ── Deploy Template Command ───────────────────────────────────────────────────

public record DeployTemplateCommand(
    string Slug,
    Guid ProjectId,
    Guid? ServerId,
    Dictionary<string, string>? EnvOverrides,
    string? EnvironmentName
) : IRequest<Result<string>>;  // returns new service ID

public class DeployTemplateCommandHandler : IRequestHandler<DeployTemplateCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    private static readonly List<AppTemplate> _templates = GetTemplatesQueryHandler.BuildTemplates();

    public DeployTemplateCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> Handle(DeployTemplateCommand request, CancellationToken ct)
    {
        var template = _templates.FirstOrDefault(t => t.Slug == request.Slug);
        if (template is null)
            return Result<string>.Failure("Template not found.", "404");

        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<string>.Failure("Project not found.", "404");

        // Create a service from the template
        var service = Service.Create(
            _currentUser.TenantId,
            request.ProjectId,
            template.Name,
            template.ServiceType,
            $"{template.DockerImage}");

        await _uow.Services.AddAsync(service, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success(service.Id.ToString());
    }
}
