using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using DeployFlow.Infrastructure.BackgroundServices;
using DeployFlow.Infrastructure.Identity;
using DeployFlow.Infrastructure.Persistence;
using DeployFlow.Infrastructure.Persistence.Repositories;
using DeployFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BC = BCrypt.Net.BCrypt;

namespace DeployFlow.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureLayer(
        this IServiceCollection services, IConfiguration configuration)
    {
        // EF Core + Oracle
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseOracle(
                configuration.GetConnectionString("DefaultConnection"),
                oracle => oracle.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // ASP.NET Core Identity — registers UserManager, RoleManager, SignInManager
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // Redis cache
        services.AddStackExchangeRedisCache(options =>
            options.Configuration = configuration.GetConnectionString("Redis"));

        // Repositories
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IDeploymentRepository, DeploymentRepository>();
        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddScoped<IDatabaseRepository, Persistence.Repositories.DatabaseRepository>();
        services.AddScoped<IPipelineRepository, Persistence.Repositories.PipelineRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantEntityRepository, TenantEntityRepository>();
        services.AddScoped<ISshKeyRepository, SshKeyRepository>();
        services.AddScoped<ICostRecordRepository, CostRecordRepository>();
        services.AddScoped<IServiceRepository, Persistence.Repositories.ServiceRepository>();
        services.AddScoped<IDomainRepository, DomainRepository>();
        services.AddScoped<IEnvVariableRepository, EnvVariableRepository>();
        services.AddScoped<INotificationConfigRepository, NotificationConfigRepository>();

        // Unit of work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Identity services
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IEncryptionService, EncryptionService>();

        // Infrastructure services
        services.AddSingleton<IDockerService, DockerService>();
        services.AddScoped<ISshService, SshService>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddScoped<IGitService, GitService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<BlueGreenDeploymentService>();
        services.AddScoped<ClusterService>();
        services.AddHttpClient("notifications");

        // Background services
        services.AddHostedService<DeploymentRunnerService>();
        services.AddHostedService<ServerHealthCheckService>();
        services.AddHostedService<AlertEvaluatorService>();
        services.AddHostedService<ContainerMetricsCollectorService>();
        services.AddHostedService<CostCalculationService>();

        return services;
    }

    public static async Task MigrateAndSeedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        await db.Database.MigrateAsync();
        await SeedAsync(db, userManager, roleManager);
    }

    private static async Task SeedAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        // ── Roles ────────────────────────────────────────────────────────
        foreach (var role in new[] { "Admin", "Developer", "Viewer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
        }

        // ── Tenant ────────────────────────────────────────────────────────
        if (!db.Tenants.Any())
        {
            db.Tenants.Add(Tenant.Create("DeployFlow Demo"));
            await db.SaveChangesAsync();
        }
        var tenantId = db.Tenants.First().Id;

        // ── Admin user ────────────────────────────────────────────────────
        const string adminEmail = "admin@deployflow.dev";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Admin User",
                Role = "Admin",
                TenantId = tenantId,
                IsActive = true,
            };
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // ── Servers ───────────────────────────────────────────────────────
        if (!db.Servers.Any())
        {
            db.Servers.AddRange(
                MakeServer(tenantId, "prod-us-east-1",  "10.0.1.10", true,  ServerProvider.Aws,          "us-east-1",  8,  32, 500),
                MakeServer(tenantId, "prod-eu-west-1",  "10.0.2.10", true,  ServerProvider.Aws,          "eu-west-1",  4,  16, 200),
                MakeServer(tenantId, "staging-server",  "10.0.3.10", true,  ServerProvider.DigitalOcean, "nyc3",       4,  8,  100),
                MakeServer(tenantId, "dev-server",      "10.0.4.10", true,  ServerProvider.Hetzner,      "nbg1",       2,  4,  80),
                MakeServer(tenantId, "backup-server",   "10.0.5.10", false, ServerProvider.Custom,       "on-prem",    4,  16, 1000),
                MakeServer(tenantId, "oracle-db-host",  "10.0.1.20", true,  ServerProvider.Aws,          "us-east-1",  8,  64, 2000)
            );
            await db.SaveChangesAsync();
        }

        // ── Projects ──────────────────────────────────────────────────────
        if (!db.Projects.Any())
        {
            db.Projects.AddRange(
                MakeProject(tenantId, "E-Commerce Platform",  "https://github.com/demo/ecommerce",   "main"),
                MakeProject(tenantId, "API Gateway",          "https://github.com/demo/api-gateway", "main"),
                MakeProject(tenantId, "Analytics Dashboard",  "https://github.com/demo/analytics",   "main"),
                MakeProject(tenantId, "Auth Service",         "https://github.com/demo/auth",        "main"),
                MakeProject(tenantId, "Mobile Backend",       "https://github.com/demo/mobile-api",  "develop"),
                MakeProject(tenantId, "Notification Worker",  "https://github.com/demo/notif",       "main"),
                MakeProject(tenantId, "Legacy CRM",           "https://github.com/demo/crm",         "legacy"),
                MakeProject(tenantId, "Data Pipeline",        "https://github.com/demo/pipeline",    "main")
            );
            await db.SaveChangesAsync();
        }

        // ── Database instances ────────────────────────────────────────────
        if (!db.Databases.Any())
        {
            db.Databases.AddRange(
                MakeDb(tenantId, "postgres-prod",      DatabaseEngine.PostgreSQL, "16.2", "10.0.1.10", 5432,  "deployflow_prod"),
                MakeDb(tenantId, "postgres-staging",   DatabaseEngine.PostgreSQL, "16.2", "10.0.3.10", 5432,  "deployflow_stg"),
                MakeDb(tenantId, "mysql-legacy",       DatabaseEngine.MySQL,      "8.0",  "10.0.4.10", 3306,  "legacy_db"),
                MakeDb(tenantId, "mongodb-analytics",  DatabaseEngine.MongoDB,    "7.0",  "10.0.2.10", 27017, "analytics"),
                MakeDb(tenantId, "redis-sessions",     DatabaseEngine.Redis,      "7.2",  "10.0.2.10", 6380,  "0"),
                MakeDb(tenantId, "mssql-reporting",    DatabaseEngine.MsSql,      "2022", "10.0.1.10", 1433,  "reporting"),
                MakeDb(tenantId, "mariadb-analytics",  DatabaseEngine.MariaDB,    "11.2", "10.0.4.10", 3307,  "analytics"),
                MakeDb(tenantId, "oracle-erp",         DatabaseEngine.Oracle,     "19c",  "10.0.1.20", 1521,  "ORCL")
            );
            await db.SaveChangesAsync();
        }

        // ── Alerts ────────────────────────────────────────────────────────
        if (!db.Alerts.Any())
        {
            db.Alerts.AddRange(
                MakeAlert(tenantId, "High CPU on prod-us-east-1", AlertSeverity.Critical, AlertStatus.Active,  "cpu_percent > 90"),
                MakeAlert(tenantId, "Memory pressure staging",    AlertSeverity.Warning,  AlertStatus.Active,   "memory_percent > 80"),
                MakeAlert(tenantId, "Disk space warning",         AlertSeverity.Warning,  AlertStatus.Resolved, "disk_percent > 75"),
                MakeAlert(tenantId, "Deployment failure spike",   AlertSeverity.Critical, AlertStatus.Active,   "failed_deployments > 3")
            );
            await db.SaveChangesAsync();
        }
    }

    // ── Factory helpers ────────────────────────────────────────────────────────

    /// Server uses private setters — use factory method then mutate through domain methods
    private static Server MakeServer(Guid tenantId, string name, string ip,
        bool online, ServerProvider provider, string region, int cpu, int mem, int disk)
    {
        var srv = Server.Create(tenantId, name, ip,
            sshUser: "ubuntu", provider: provider, region: region,
            cpuCount: cpu, memoryGb: mem, diskGb: disk);
        if (online) srv.SetOnline("27.3.1");
        else        srv.SetOffline();
        srv.UpdateMetrics(
            Random.Shared.Next(10, 75),
            Random.Shared.Next(20, 80),
            Random.Shared.Next(10, 60),
            Random.Shared.Next(0, 12));
        return srv;
    }

    /// Project uses private setters — use factory method
    private static Project MakeProject(Guid tenantId, string name, string repoUrl, string branch)
        => Project.Create(tenantId, name,
               repositoryUrl: repoUrl,
               repositoryBranch: branch,
               buildCommand: "npm run build",
               startCommand: "npm start",
               installCommand: "npm install",
               framework: "nextjs",
               autoDeployEnabled: true);

    /// DatabaseInstance has public setters — object initializer is fine
    private static DatabaseInstance MakeDb(Guid tenantId, string name, DatabaseEngine engine,
        string version, string host, int port, string dbName) => new()
    {
        TenantId = tenantId, Name = name, Engine = engine, Version = version,
        ServerId = Guid.Empty,
        Host = host, Port = port, DatabaseName = dbName, Username = "app_user",
        PasswordEncrypted = BC.HashPassword("placeholder"),
        Status = DatabaseInstanceStatus.Running, StorageGb = Random.Shared.Next(10, 500),
        BackupEnabled = true,
        LastBackupAt = DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 24)),
        CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(10, 180)),
        UpdatedAt = DateTime.UtcNow,
    };

    /// Alert has public setters — object initializer is fine
    private static Alert MakeAlert(Guid tenantId, string name,
        AlertSeverity severity, AlertStatus status, string condition) => new()
    {
        TenantId = tenantId, Name = name, Source = "system",
        Severity = severity, Status = status, Condition = condition,
        CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 14)),
        UpdatedAt = DateTime.UtcNow,
    };
}
