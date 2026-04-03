using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeployFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint910Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    DockerImage = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    ComposeYaml = table.Column<string>(type: "NCLOB", nullable: true),
                    LogoUrl = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    DocumentationUrl = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    GithubUrl = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    ServiceType = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    DefaultPort = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RequiresDatabase = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DefaultDatabaseType = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    EnvVariables = table.Column<string>(type: "NCLOB", nullable: false),
                    DeployCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IsOfficial = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "compose_stacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ServerId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    ComposeYaml = table.Column<string>(type: "NCLOB", nullable: false),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ServiceCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LastDeployedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LastError = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    EnvironmentName = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compose_stacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbound_webhook_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    Events = table.Column<string>(type: "NCLOB", nullable: false),
                    Secret = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    DeliveryCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FailureCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LastDeliveredAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LastResponseStatus = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbound_webhook_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreviewEnvironments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PrNumber = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    PrTitle = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Branch = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Url = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Status = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    MergedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreviewEnvironments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectEnvironments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Slug = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    IsDefault = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    IsProduction = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    Order = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEnvironments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provisioning_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Region = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    Size = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    Os = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SshKeyId_External = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SshKeyId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ProviderServerId = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    AssignedIpAddress = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    PlanOutput = table.Column<string>(type: "NCLOB", nullable: true),
                    ErrorMessage = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    CreatedServerId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
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
                    table.PrimaryKey("PK_provisioning_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recovery_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Trigger = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    MaxRetries = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    RetryCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    TargetServerId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    TargetProjectId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "traefik_routers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Rule = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    ServiceName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Entrypoints = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    TlsEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CertResolver = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ServerId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    DomainId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_traefik_routers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_templates_Category",
                table: "app_templates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_app_templates_Slug",
                table: "app_templates",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compose_stacks_ProjectId",
                table: "compose_stacks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_compose_stacks_TenantId",
                table: "compose_stacks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_compose_stacks_TenantId_Status",
                table: "compose_stacks",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_outbound_webhook_configs_TenantId",
                table: "outbound_webhook_configs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_webhook_configs_TenantId_IsEnabled",
                table: "outbound_webhook_configs",
                columns: new[] { "TenantId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_provisioning_jobs_TenantId",
                table: "provisioning_jobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_provisioning_jobs_TenantId_Status",
                table: "provisioning_jobs",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_recovery_rules_TenantId",
                table: "recovery_rules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_recovery_rules_TenantId_IsEnabled",
                table: "recovery_rules",
                columns: new[] { "TenantId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_traefik_routers_TenantId",
                table: "traefik_routers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_traefik_routers_TenantId_IsEnabled",
                table: "traefik_routers",
                columns: new[] { "TenantId", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_templates");

            migrationBuilder.DropTable(
                name: "compose_stacks");

            migrationBuilder.DropTable(
                name: "outbound_webhook_configs");

            migrationBuilder.DropTable(
                name: "PreviewEnvironments");

            migrationBuilder.DropTable(
                name: "ProjectEnvironments");

            migrationBuilder.DropTable(
                name: "provisioning_jobs");

            migrationBuilder.DropTable(
                name: "recovery_rules");

            migrationBuilder.DropTable(
                name: "traefik_routers");
        }
    }
}
