using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeployFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialOracleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Severity = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    ResourceId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    ResourceType = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Condition = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    Threshold = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    TriggeredAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    AcknowledgedById = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ConditionsJson = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    UserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    UserName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ResourceType = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ResourceName = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    IpAddress = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    MetadataJson = table.Column<string>(type: "NCLOB", nullable: true),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cost_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ResourceType = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ResourceName = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    ResourceId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Amount = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                    Period = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "database_instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Engine = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ServerId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Host = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    Port = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DatabaseName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PasswordEncrypted = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    StorageGb = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    BackupEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    BackupSchedule = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    BackupRetentionDays = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LastBackupAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    Tags = table.Column<string>(type: "NCLOB", nullable: false),
                    ContainerId = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_instances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Domains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ServiceId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsWildcard = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    SslEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    SslExpiresAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    SslProvider = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DnsVerified = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    RedirectWww = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Channel = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ConfigJson = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    EventsJson = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    UserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Token = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    IsRevoked = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ReplacedByToken = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CreatedByIp = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    RevokedByIp = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "server_metrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ServerId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CpuUsagePercent = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    MemoryUsageBytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    MemoryTotalBytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DiskUsageBytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DiskTotalBytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    NetworkRxBytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    NetworkTxBytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ActiveContainers = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LoadAverage1m = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_metrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "servers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Hostname = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    IpAddress = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SshPort = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SshUser = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    SshKeyId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Provider = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Region = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    Os = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Arch = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CpuCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MemoryGb = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DiskGb = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CpuUsagePercent = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    MemoryUsagePercent = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    DiskUsagePercent = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    ActiveContainers = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IsSwarmManager = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IsSwarmWorker = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    KubernetesEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DockerVersion = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Tags = table.Column<string>(type: "NCLOB", nullable: false),
                    LastHealthCheckAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    SwarmNodeId = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ssh_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PublicKey = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: false),
                    PrivateKeyEncrypted = table.Column<string>(type: "NCLOB", nullable: false),
                    Fingerprint = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ssh_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    LogoUrl = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    IsActive = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    Plan = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    MaxProjects = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MaxServers = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MaxDeployments = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    FullName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    AvatarUrl = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    Role = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    IsActive = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    TwoFactorEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    RefreshToken = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    RefreshTokenExpiry = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UserName = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    PasswordHash = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatabaseBackups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    DatabaseInstanceId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    FileName = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    StoragePath = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    SizeBytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    Status = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    IsAutomatic = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseBackups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatabaseBackups_database_instances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "database_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    RoleId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ClaimType = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    ClaimValue = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_claims_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    RepositoryUrl = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    RepositoryBranch = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    ServerId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    EnvironmentId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    AutoDeployEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    BranchDeployEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    PreviewDeployEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    BuildCommand = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    InstallCommand = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    StartCommand = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    DockerfilePath = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    Framework = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    CustomDomain = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    OutputDirectory = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    RootDirectory = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Port = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    HealthCheckPath = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    HealthCheckTimeout = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LastDeployedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    DeploymentCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LastDeploymentId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    LastDeploymentStatus = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    Tags = table.Column<string>(type: "NCLOB", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projects_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    UserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ClaimType = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    ClaimValue = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_claims_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    UserId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_logins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_user_logins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    RoleId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    LoginProvider = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    Value = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_user_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Trigger = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    CommitSha = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    CommitMessage = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CommitAuthor = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    Branch = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    EnvironmentId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    ServerId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    TriggeredBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    ImageTag = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    DurationSeconds = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    Url = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    PreviousDeploymentId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsRollback = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    Metadata = table.Column<string>(type: "NCLOB", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_deployments_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    Trigger = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    CronExpression = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LastDuration = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: true),
                    TotalRuns = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SuccessRuns = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FailedRuns = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pipelines_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Type = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Image = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Tag = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Replicas = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DomainId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    ContainerId = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CpuLimit = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    MemoryLimit = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CpuRequest = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    MemoryRequest = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    HealthCheckPath = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    HealthCheckInterval = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HealthCheckTimeout = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HealthCheckRetries = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HealthCheckStartPeriod = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deployment_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Message = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: false),
                    Level = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    Stream = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployment_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_deployment_logs_deployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "deployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PipelineId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Order = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RunParallel = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineStages_pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvVariables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Key = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Value = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Type = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ServiceId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    ProjectId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsShared = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvVariables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvVariables_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EnvVariables_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PipelineSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PipelineStageId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Type = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Command = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Image = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    ConfigJson = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: true),
                    Timeout = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineSteps_PipelineStages_PipelineStageId",
                        column: x => x.PipelineStageId,
                        principalTable: "PipelineStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_TenantId",
                table: "alerts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_TenantId",
                table: "audit_logs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_records_TenantId",
                table: "cost_records",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_database_instances_TenantId",
                table: "database_instances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseBackups_DatabaseInstanceId",
                table: "DatabaseBackups",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_deployment_logs_DeploymentId",
                table: "deployment_logs",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_deployment_logs_Timestamp",
                table: "deployment_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_deployments_ProjectId",
                table: "deployments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_deployments_Status",
                table: "deployments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_deployments_TenantId",
                table: "deployments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvVariables_ProjectId",
                table: "EnvVariables",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvVariables_ServiceId",
                table: "EnvVariables",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_pipelines_ProjectId",
                table: "pipelines",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_pipelines_TenantId",
                table: "pipelines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineStages_PipelineId",
                table: "PipelineStages",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineSteps_PipelineStageId",
                table: "PipelineSteps",
                column: "PipelineStageId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_ServerId",
                table: "projects",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_TenantId",
                table: "projects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_TenantId_Slug",
                table: "projects",
                columns: new[] { "TenantId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_claims_RoleId",
                table: "role_claims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "NormalizedName",
                unique: true,
                filter: "\"NormalizedName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_server_metrics_ServerId_Timestamp",
                table: "server_metrics",
                columns: new[] { "ServerId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_servers_TenantId",
                table: "servers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_ProjectId",
                table: "Services",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ssh_keys_TenantId",
                table: "ssh_keys",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_claims_UserId",
                table: "user_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_logins_UserId",
                table: "user_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_users_RefreshToken",
                table: "users",
                column: "RefreshToken");

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId",
                table: "users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "NormalizedUserName",
                unique: true,
                filter: "\"NormalizedUserName\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "cost_records");

            migrationBuilder.DropTable(
                name: "DatabaseBackups");

            migrationBuilder.DropTable(
                name: "deployment_logs");

            migrationBuilder.DropTable(
                name: "Domains");

            migrationBuilder.DropTable(
                name: "EnvVariables");

            migrationBuilder.DropTable(
                name: "NotificationConfigs");

            migrationBuilder.DropTable(
                name: "PipelineSteps");

            migrationBuilder.DropTable(
                name: "project_tags");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "server_metrics");

            migrationBuilder.DropTable(
                name: "ssh_keys");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "database_instances");

            migrationBuilder.DropTable(
                name: "deployments");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "PipelineStages");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "pipelines");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "servers");
        }
    }
}
