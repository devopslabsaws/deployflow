using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeployFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAutoscalingAndBlueGreen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CpuTargetPercentage",
                table: "Services",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastScaledAt",
                table: "Services",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastScalingAction",
                table: "Services",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastScalingReason",
                table: "Services",
                type: "NVARCHAR2(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxReplicas",
                table: "Services",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MemoryTargetPercentage",
                table: "Services",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinReplicas",
                table: "Services",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "CloudflareSshDomain",
                table: "servers",
                type: "NVARCHAR2(2000)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CloudflareTunnelManual",
                table: "servers",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CloudflareTunnelToken",
                table: "servers",
                type: "NVARCHAR2(2000)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeleteUnusedNetworks",
                table: "servers",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DeleteUnusedVolumes",
                table: "servers",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DisableAppImageRetention",
                table: "servers",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DockerCleanupForce",
                table: "servers",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DockerCleanupFrequency",
                table: "servers",
                type: "NVARCHAR2(2000)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActiveSlot",
                table: "projects",
                type: "NVARCHAR2(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "blue");

            migrationBuilder.AddColumn<string>(
                name: "BlueContainerName",
                table: "projects",
                type: "NVARCHAR2(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GreenContainerName",
                table: "projects",
                type: "NVARCHAR2(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "S3DestinationId",
                table: "DatabaseBackups",
                type: "RAW(16)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "alerts",
                type: "NVARCHAR2(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(2000)");

            migrationBuilder.CreateTable(
                name: "backup_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    DatabaseInstanceId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CronExpression = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false, defaultValue: "0 2 * * *"),
                    RetentionDays = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    S3DestinationId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    StorageLocation = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false, defaultValue: "local"),
                    LastRunAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Strategy = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IsActive = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clusters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeployWebhooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Token = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    GitHubSecret = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    GitLabSecret = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    BitbucketSecret = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    GiteaSecret = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    IsActive = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployWebhooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_run_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PipelineRunId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    Level = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    StageName = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    StepName = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: true),
                    Message = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: false),
                    Sequence = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_run_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PipelineId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    StageCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    StepCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TriggeredBy = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourcePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    UserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ResourceType = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ResourceId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Actions = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourcePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "restore_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    DatabaseInstanceId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    BackupId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TargetDatabaseName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    ProgressPercent = table.Column<float>(type: "BINARY_FLOAT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restore_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "s3_destinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    Endpoint = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    BucketName = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    AccessKeyIdEncrypted = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    SecretAccessKeyEncrypted = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    Region = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    IsDefault = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_s3_destinations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Command = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Frequency = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ContainerName = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    IsActive = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LastRunStatus = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    LastRunOutput = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "volumes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ServerId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    MountPath = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    SizeBytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    Driver = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false, defaultValue: "local"),
                    Status = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    DockerName = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_volumes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_servers_TenantId_Status",
                table: "servers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_projects_TenantId_UpdatedAt",
                table: "projects",
                columns: new[] { "TenantId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pipelines_TenantId_CreatedAt",
                table: "pipelines",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_deployments_ProjectId_CreatedAt",
                table: "deployments",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_deployments_TenantId_CreatedAt",
                table: "deployments",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_deployments_TenantId_Status_CreatedAt",
                table: "deployments",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_deployment_logs_DeploymentId_Timestamp",
                table: "deployment_logs",
                columns: new[] { "DeploymentId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_cost_records_TenantId_RecordedAt",
                table: "cost_records",
                columns: new[] { "TenantId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_TenantId_Source_ResourceId_Status",
                table: "alerts",
                columns: new[] { "TenantId", "Source", "ResourceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_backup_policies_S3DestinationId",
                table: "backup_policies",
                column: "S3DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_backup_policies_TenantId_DatabaseInstanceId",
                table: "backup_policies",
                columns: new[] { "TenantId", "DatabaseInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_run_logs_PipelineRunId_Sequence",
                table: "pipeline_run_logs",
                columns: new[] { "PipelineRunId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_runs_PipelineId_StartedAt",
                table: "pipeline_runs",
                columns: new[] { "PipelineId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_runs_TenantId",
                table: "pipeline_runs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_restore_jobs_BackupId",
                table: "restore_jobs",
                column: "BackupId");

            migrationBuilder.CreateIndex(
                name: "IX_restore_jobs_TenantId",
                table: "restore_jobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_restore_jobs_TenantId_DatabaseInstanceId",
                table: "restore_jobs",
                columns: new[] { "TenantId", "DatabaseInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_s3_destinations_TenantId",
                table: "s3_destinations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_s3_destinations_TenantId_IsDefault",
                table: "s3_destinations",
                columns: new[] { "TenantId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_volumes_ServerId",
                table: "volumes",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_volumes_TenantId",
                table: "volumes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_volumes_TenantId_Name",
                table: "volumes",
                columns: new[] { "TenantId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backup_policies");

            migrationBuilder.DropTable(
                name: "Clusters");

            migrationBuilder.DropTable(
                name: "DeployWebhooks");

            migrationBuilder.DropTable(
                name: "pipeline_run_logs");

            migrationBuilder.DropTable(
                name: "pipeline_runs");

            migrationBuilder.DropTable(
                name: "ResourcePermissions");

            migrationBuilder.DropTable(
                name: "restore_jobs");

            migrationBuilder.DropTable(
                name: "s3_destinations");

            migrationBuilder.DropTable(
                name: "ScheduledTasks");

            migrationBuilder.DropTable(
                name: "volumes");

            migrationBuilder.DropIndex(
                name: "IX_servers_TenantId_Status",
                table: "servers");

            migrationBuilder.DropIndex(
                name: "IX_projects_TenantId_UpdatedAt",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_pipelines_TenantId_CreatedAt",
                table: "pipelines");

            migrationBuilder.DropIndex(
                name: "IX_deployments_ProjectId_CreatedAt",
                table: "deployments");

            migrationBuilder.DropIndex(
                name: "IX_deployments_TenantId_CreatedAt",
                table: "deployments");

            migrationBuilder.DropIndex(
                name: "IX_deployments_TenantId_Status_CreatedAt",
                table: "deployments");

            migrationBuilder.DropIndex(
                name: "IX_deployment_logs_DeploymentId_Timestamp",
                table: "deployment_logs");

            migrationBuilder.DropIndex(
                name: "IX_cost_records_TenantId_RecordedAt",
                table: "cost_records");

            migrationBuilder.DropIndex(
                name: "IX_alerts_TenantId_Source_ResourceId_Status",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "CpuTargetPercentage",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "LastScaledAt",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "LastScalingAction",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "LastScalingReason",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "MaxReplicas",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "MemoryTargetPercentage",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "MinReplicas",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "CloudflareSshDomain",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "CloudflareTunnelManual",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "CloudflareTunnelToken",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "DeleteUnusedNetworks",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "DeleteUnusedVolumes",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "DisableAppImageRetention",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "DockerCleanupForce",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "DockerCleanupFrequency",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "ActiveSlot",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "BlueContainerName",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "GreenContainerName",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "S3DestinationId",
                table: "DatabaseBackups");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "alerts",
                type: "NVARCHAR2(2000)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(450)");
        }
    }
}
