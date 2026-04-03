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
        new() { Name = "Nginx", Slug = "nginx", Description = "High-performance HTTP server and reverse proxy.", Category = "web", DockerImage = "nginx:alpine", LogoUrl = "/logos/nginx.svg", ServiceType = ServiceType.Web, DefaultPort = 80, RequiresDatabase = false, IsOfficial = true, DeployCount = 5200, EnvVariables = [] },
        new() { Name = "Redis", Slug = "redis", Description = "In-memory data store for caching and pub/sub.", Category = "database", DockerImage = "redis:7-alpine", LogoUrl = "/logos/redis.svg", ServiceType = ServiceType.Cache, DefaultPort = 6379, RequiresDatabase = false, IsOfficial = true, DeployCount = 4800, EnvVariables = [
                new() { Key = "REDIS_PASSWORD", Required = false, IsSecret = true },
            ] },
        new() { Name = "Umami", Slug = "umami", Description = "Simple, fast, privacy-focused website analytics.", Category = "analytics", DockerImage = "ghcr.io/umami-software/umami:postgresql-latest", LogoUrl = "/logos/umami.svg", GithubUrl = "https://github.com/umami-software/umami", ServiceType = ServiceType.Web, DefaultPort = 3000, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = false, DeployCount = 410, EnvVariables = [
                new() { Key = "DATABASE_URL", Required = true, IsSecret = true },
                new() { Key = "APP_SECRET", Required = true, IsSecret = true },
            ] },
        // ── CMS ──────────────────────────────────────────────────────────────
        new() { Name = "Directus", Slug = "directus", Description = "Flexible headless CMS and data platform with instant REST & GraphQL APIs.", Category = "cms", DockerImage = "directus/directus:latest", LogoUrl = "/logos/directus.svg", GithubUrl = "https://github.com/directus/directus", DocumentationUrl = "https://docs.directus.io", ServiceType = ServiceType.Api, DefaultPort = 8055, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 870,
            EnvVariables = [
                new() { Key = "SECRET", Required = true, IsSecret = true, Description = "Random secret for hashing" },
                new() { Key = "ADMIN_EMAIL", DefaultValue = "admin@example.com", Required = true },
                new() { Key = "ADMIN_PASSWORD", Required = true, IsSecret = true },
                new() { Key = "DB_CLIENT", DefaultValue = "pg", Required = true },
                new() { Key = "DB_CONNECTION_STRING", Required = true, IsSecret = true },
            ] },
        new() { Name = "Strapi", Slug = "strapi", Description = "Open-source headless CMS with customizable API and admin UI.", Category = "cms", DockerImage = "strapi/strapi:latest", LogoUrl = "/logos/strapi.svg", GithubUrl = "https://github.com/strapi/strapi", DocumentationUrl = "https://docs.strapi.io", ServiceType = ServiceType.Api, DefaultPort = 1337, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 1100,
            EnvVariables = [
                new() { Key = "APP_KEYS", Required = true, IsSecret = true },
                new() { Key = "API_TOKEN_SALT", Required = true, IsSecret = true },
                new() { Key = "DATABASE_CLIENT", DefaultValue = "postgres", Required = true },
            ] },
        // ── Database ─────────────────────────────────────────────────────────
        new() { Name = "PostgreSQL", Slug = "postgres", Description = "The world's most advanced open-source relational database.", Category = "database", DockerImage = "postgres:16-alpine", LogoUrl = "/logos/postgres.svg", DocumentationUrl = "https://www.postgresql.org/docs/", ServiceType = ServiceType.Database, DefaultPort = 5432, RequiresDatabase = false, IsOfficial = true, DeployCount = 9200,
            EnvVariables = [
                new() { Key = "POSTGRES_DB", DefaultValue = "mydb", Required = true },
                new() { Key = "POSTGRES_USER", DefaultValue = "admin", Required = true },
                new() { Key = "POSTGRES_PASSWORD", Required = true, IsSecret = true },
            ] },
        new() { Name = "MySQL", Slug = "mysql", Description = "The most widely used open-source relational database.", Category = "database", DockerImage = "mysql:8.0", LogoUrl = "/logos/mysql.svg", DocumentationUrl = "https://dev.mysql.com/doc/", ServiceType = ServiceType.Database, DefaultPort = 3306, RequiresDatabase = false, IsOfficial = true, DeployCount = 7800,
            EnvVariables = [
                new() { Key = "MYSQL_DATABASE", DefaultValue = "mydb", Required = true },
                new() { Key = "MYSQL_USER", DefaultValue = "admin", Required = true },
                new() { Key = "MYSQL_PASSWORD", Required = true, IsSecret = true },
                new() { Key = "MYSQL_ROOT_PASSWORD", Required = true, IsSecret = true },
            ] },
        new() { Name = "MariaDB", Slug = "mariadb", Description = "MySQL-compatible open-source database server.", Category = "database", DockerImage = "mariadb:11", LogoUrl = "/logos/mariadb.svg", ServiceType = ServiceType.Database, DefaultPort = 3306, RequiresDatabase = false, IsOfficial = true, DeployCount = 3100,
            EnvVariables = [
                new() { Key = "MARIADB_DATABASE", DefaultValue = "mydb", Required = true },
                new() { Key = "MARIADB_USER", DefaultValue = "admin", Required = true },
                new() { Key = "MARIADB_PASSWORD", Required = true, IsSecret = true },
                new() { Key = "MARIADB_ROOT_PASSWORD", Required = true, IsSecret = true },
            ] },
        new() { Name = "MongoDB", Slug = "mongodb", Description = "Document-oriented NoSQL database for flexible, scalable data.", Category = "database", DockerImage = "mongo:7.0", LogoUrl = "/logos/mongodb.svg", ServiceType = ServiceType.Database, DefaultPort = 27017, RequiresDatabase = false, IsOfficial = true, DeployCount = 5400,
            EnvVariables = [
                new() { Key = "MONGO_INITDB_ROOT_USERNAME", DefaultValue = "admin", Required = true },
                new() { Key = "MONGO_INITDB_ROOT_PASSWORD", Required = true, IsSecret = true },
            ] },
        // ── Analytics ────────────────────────────────────────────────────────
        new() { Name = "Matomo", Slug = "matomo", Description = "Open-source web analytics platform — GDPR compliant owner of your data.", Category = "analytics", DockerImage = "matomo:fpm-alpine", LogoUrl = "/logos/matomo.svg", GithubUrl = "https://github.com/matomo-org/matomo", ServiceType = ServiceType.Web, DefaultPort = 80, RequiresDatabase = true, DefaultDatabaseType = "MySQL", IsOfficial = true, DeployCount = 620,
            EnvVariables = [
                new() { Key = "MATOMO_DATABASE_HOST", DefaultValue = "db", Required = true },
                new() { Key = "MATOMO_DATABASE_ADAPTER", DefaultValue = "mysql", Required = false },
            ] },
        new() { Name = "PostHog", Slug = "posthog", Description = "Open-source product analytics platform — track events, funnels, heatmaps.", Category = "analytics", DockerImage = "posthog/posthog:latest", LogoUrl = "/logos/posthog.svg", GithubUrl = "https://github.com/PostHog/posthog", ServiceType = ServiceType.Web, DefaultPort = 8000, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 380,
            EnvVariables = [
                new() { Key = "SECRET_KEY", Required = true, IsSecret = true },
                new() { Key = "DATABASE_URL", Required = true, IsSecret = true },
            ] },
        // ── Monitoring ───────────────────────────────────────────────────────
        new() { Name = "Prometheus", Slug = "prometheus", Description = "Monitoring system and time-series database for metrics.", Category = "monitoring", DockerImage = "prom/prometheus:latest", LogoUrl = "/logos/prometheus.svg", GithubUrl = "https://github.com/prometheus/prometheus", ServiceType = ServiceType.Web, DefaultPort = 9090, RequiresDatabase = false, IsOfficial = true, DeployCount = 2800, EnvVariables = [] },
        new() { Name = "Uptime Kuma", Slug = "uptime-kuma", Description = "Fancy self-hosted monitoring tool with status pages and alerting.", Category = "monitoring", DockerImage = "louislam/uptime-kuma:1", LogoUrl = "/logos/uptime-kuma.svg", GithubUrl = "https://github.com/louislam/uptime-kuma", ServiceType = ServiceType.Web, DefaultPort = 3001, RequiresDatabase = false, IsOfficial = true, DeployCount = 3400, EnvVariables = [] },
        new() { Name = "Netdata", Slug = "netdata", Description = "Real-time system performance monitoring with rich dashboards.", Category = "monitoring", DockerImage = "netdata/netdata:latest", LogoUrl = "/logos/netdata.svg", GithubUrl = "https://github.com/netdata/netdata", ServiceType = ServiceType.Web, DefaultPort = 19999, RequiresDatabase = false, IsOfficial = true, DeployCount = 1600, EnvVariables = [] },
        // ── Automation ───────────────────────────────────────────────────────
        new() { Name = "Gitea", Slug = "gitea", Description = "Lightweight self-hosted Git service — like GitHub but on your server.", Category = "automation", DockerImage = "gitea/gitea:latest", LogoUrl = "/logos/gitea.svg", GithubUrl = "https://github.com/go-gitea/gitea", ServiceType = ServiceType.Web, DefaultPort = 3000, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 2200,
            EnvVariables = [
                new() { Key = "GITEA__database__DB_TYPE", DefaultValue = "postgres", Required = false },
                new() { Key = "GITEA__database__HOST", DefaultValue = "db:5432", Required = false },
            ] },
        new() { Name = "Portainer CE", Slug = "portainer", Description = "Lightweight Docker management UI to manage containers visually.", Category = "automation", DockerImage = "portainer/portainer-ce:latest", LogoUrl = "/logos/portainer.svg", GithubUrl = "https://github.com/portainer/portainer", ServiceType = ServiceType.Web, DefaultPort = 9000, RequiresDatabase = false, IsOfficial = true, DeployCount = 4200, EnvVariables = [] },
        new() { Name = "Drone CI", Slug = "drone", Description = "Container-native continuous integration and delivery platform.", Category = "automation", DockerImage = "drone/drone:2", LogoUrl = "/logos/drone.svg", GithubUrl = "https://github.com/harness/drone", ServiceType = ServiceType.Web, DefaultPort = 80, RequiresDatabase = false, IsOfficial = true, DeployCount = 780,
            EnvVariables = [
                new() { Key = "DRONE_GITEA_CLIENT_ID", Required = false },
                new() { Key = "DRONE_GITEA_CLIENT_SECRET", Required = false, IsSecret = true },
                new() { Key = "DRONE_RPC_SECRET", Required = true, IsSecret = true },
                new() { Key = "DRONE_SERVER_HOST", Required = true },
            ] },
        // ── Storage ──────────────────────────────────────────────────────────
        new() { Name = "Nextcloud", Slug = "nextcloud", Description = "Self-hosted file storage, calendar, contacts and productivity suite.", Category = "storage", DockerImage = "nextcloud:27-apache", LogoUrl = "/logos/nextcloud.svg", GithubUrl = "https://github.com/nextcloud/server", ServiceType = ServiceType.Web, DefaultPort = 80, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 2100,
            EnvVariables = [
                new() { Key = "NEXTCLOUD_ADMIN_USER", DefaultValue = "admin", Required = true },
                new() { Key = "NEXTCLOUD_ADMIN_PASSWORD", Required = true, IsSecret = true },
                new() { Key = "POSTGRES_DB", DefaultValue = "nextcloud", Required = false },
            ] },
        // ── Communication ────────────────────────────────────────────────────
        new() { Name = "Rocket.Chat", Slug = "rocketchat", Description = "Open-source team communication platform — Slack alternative.", Category = "communication", DockerImage = "rocket.chat:6", LogoUrl = "/logos/rocketchat.svg", GithubUrl = "https://github.com/RocketChat/Rocket.Chat", ServiceType = ServiceType.Web, DefaultPort = 3000, RequiresDatabase = true, DefaultDatabaseType = "MongoDB", IsOfficial = true, DeployCount = 890,
            EnvVariables = [
                new() { Key = "MONGO_URL", Required = true, IsSecret = true },
                new() { Key = "ROOT_URL", Required = true, Description = "Your public website URL" },
            ] },
        // ── Backend ──────────────────────────────────────────────────────────
        new() { Name = "Appwrite", Slug = "appwrite", Description = "Open-source Firebase alternative — auth, databases, storage, functions.", Category = "backend", DockerImage = "appwrite/appwrite:latest", LogoUrl = "/logos/appwrite.svg", GithubUrl = "https://github.com/appwrite/appwrite", ServiceType = ServiceType.Api, DefaultPort = 80, RequiresDatabase = false, IsOfficial = true, DeployCount = 960,
            EnvVariables = [
                new() { Key = "_APP_ENV", DefaultValue = "production", Required = false },
                new() { Key = "_APP_OPENSSL_KEY_V1", Required = true, IsSecret = true, Description = "32+ char random string" },
                new() { Key = "_APP_DOMAIN", Required = true, Description = "Your domain name" },
            ] },
        new() { Name = "Hasura", Slug = "hasura", Description = "Instant GraphQL & REST APIs on Postgres with real-time subscriptions.", Category = "backend", DockerImage = "hasura/graphql-engine:v2.38.0", LogoUrl = "/logos/hasura.svg", GithubUrl = "https://github.com/hasura/graphql-engine", ServiceType = ServiceType.Api, DefaultPort = 8080, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 1340,
            EnvVariables = [
                new() { Key = "HASURA_GRAPHQL_DATABASE_URL", Required = true, IsSecret = true },
                new() { Key = "HASURA_GRAPHQL_ADMIN_SECRET", Required = true, IsSecret = true },
                new() { Key = "HASURA_GRAPHQL_ENABLE_CONSOLE", DefaultValue = "true", Required = false },
            ] },
        new() { Name = "NocoDB", Slug = "nocodb", Description = "No-code spreadsheet platform on top of your database.", Category = "backend", DockerImage = "nocodb/nocodb:latest", LogoUrl = "/logos/nocodb.svg", GithubUrl = "https://github.com/nocodb/nocodb", ServiceType = ServiceType.Web, DefaultPort = 8080, RequiresDatabase = false, IsOfficial = true, DeployCount = 720,
            EnvVariables = [
                new() { Key = "NC_DB", Required = false, Description = "Optional external DB connection string" },
                new() { Key = "NC_AUTH_JWT_SECRET", Required = false, IsSecret = true },
            ] },
        new() { Name = "Keycloak", Slug = "keycloak", Description = "Enterprise-grade open-source identity and access management.", Category = "backend", DockerImage = "quay.io/keycloak/keycloak:latest", LogoUrl = "/logos/keycloak.svg", DocumentationUrl = "https://www.keycloak.org/documentation", ServiceType = ServiceType.Api, DefaultPort = 8080, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 880,
            EnvVariables = [
                new() { Key = "KC_DB", DefaultValue = "postgres", Required = false },
                new() { Key = "KC_DB_URL", Required = false, IsSecret = true },
                new() { Key = "KEYCLOAK_ADMIN", DefaultValue = "admin", Required = true },
                new() { Key = "KEYCLOAK_ADMIN_PASSWORD", Required = true, IsSecret = true },
            ] },
        // ── Productivity ─────────────────────────────────────────────────────
        new() { Name = "Plane", Slug = "plane", Description = "Open-source project planning tool — Jira & Linear alternative.", Category = "productivity", DockerImage = "makeplane/plane-frontend:latest", LogoUrl = "/logos/plane.svg", GithubUrl = "https://github.com/makeplane/plane", ServiceType = ServiceType.Web, DefaultPort = 3000, RequiresDatabase = true, DefaultDatabaseType = "PostgreSQL", IsOfficial = true, DeployCount = 490,
            EnvVariables = [
                new() { Key = "SECRET_KEY", Required = true, IsSecret = true },
                new() { Key = "DATABASE_URL", Required = true, IsSecret = true },
                new() { Key = "WEB_URL", Required = true, Description = "Your public website URL" },
            ] },
        new() { Name = "GitLab CE", Slug = "gitlabce", Description = "Complete DevOps platform — Git repos, CI/CD pipelines, issue tracking.", Category = "productivity", DockerImage = "gitlab/gitlab-ce:latest", LogoUrl = "/logos/gitlabce.svg", ServiceType = ServiceType.Web, DefaultPort = 80, RequiresDatabase = false, IsOfficial = true, DeployCount = 1800,
            EnvVariables = [
                new() { Key = "GITLAB_ROOT_PASSWORD", Required = true, IsSecret = true },
                new() { Key = "EXTERNAL_URL", Required = true, Description = "Your public domain (e.g. https://gitlab.example.com)" },
            ] },
        // ── Web / Infrastructure ─────────────────────────────────────────────
        new() { Name = "Traefik", Slug = "traefik", Description = "Modern cloud-native reverse proxy and load balancer with auto HTTPS.", Category = "web", DockerImage = "traefik:v3.0", LogoUrl = "/logos/traefik.svg", GithubUrl = "https://github.com/traefik/traefik", ServiceType = ServiceType.Web, DefaultPort = 80, RequiresDatabase = false, IsOfficial = true, DeployCount = 2900,
            EnvVariables = [
                new() { Key = "TRAEFIK_API_INSECURE", DefaultValue = "true", Required = false },
                new() { Key = "TRAEFIK_ENTRYPOINTS_WEB_ADDRESS", DefaultValue = ":80", Required = false },
            ] },
        new() { Name = "Vaultwarden", Slug = "vaultwarden", Description = "Lightweight Bitwarden server implementation — self-hosted password manager.", Category = "backend", DockerImage = "vaultwarden/server:latest", LogoUrl = "/logos/vaultwarden.svg", GithubUrl = "https://github.com/dani-garcia/vaultwarden", ServiceType = ServiceType.Web, DefaultPort = 80, RequiresDatabase = false, IsOfficial = true, DeployCount = 1950,
            EnvVariables = [
                new() { Key = "ADMIN_TOKEN", Required = true, IsSecret = true, Description = "Admin panel access token" },
                new() { Key = "SIGNUPS_ALLOWED", DefaultValue = "true", Required = false },
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
