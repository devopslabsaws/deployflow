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
            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""Services"" ADD ""CpuTargetPercentage"" NUMBER(10)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""Services"" ADD ""LastScaledAt"" TIMESTAMP(7)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""Services"" ADD ""LastScalingAction"" NVARCHAR2(50)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""Services"" ADD ""LastScalingReason"" NVARCHAR2(1000)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""Services"" ADD ""MaxReplicas"" NUMBER(10) DEFAULT 1 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""Services"" ADD ""MemoryTargetPercentage"" NUMBER(10)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""Services"" ADD ""MinReplicas"" NUMBER(10) DEFAULT 1 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""servers"" ADD ""CloudflareSshDomain"" NVARCHAR2(2000)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""servers"" ADD ""CloudflareTunnelManual"" NUMBER(1) DEFAULT 0 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""servers"" ADD ""CloudflareTunnelToken"" NVARCHAR2(2000)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""servers"" ADD ""DeleteUnusedNetworks"" NUMBER(1) DEFAULT 0 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""servers"" ADD ""DeleteUnusedVolumes"" NUMBER(1) DEFAULT 0 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""servers"" ADD ""DisableAppImageRetention"" NUMBER(1) DEFAULT 0 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""servers"" ADD ""DockerCleanupForce"" NUMBER(1) DEFAULT 0 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""servers"" ADD ""DockerCleanupFrequency"" NVARCHAR2(2000) DEFAULT ''1h'' NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""projects"" ADD ""ActiveSlot"" NVARCHAR2(10) DEFAULT ''blue'' NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""projects"" ADD ""BlueContainerName"" NVARCHAR2(300)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""projects"" ADD ""GreenContainerName"" NVARCHAR2(300)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""DatabaseBackups"" ADD ""S3DestinationId"" RAW(16)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""alerts"" MODIFY ""Source"" NVARCHAR2(450) NOT NULL';
EXCEPTION WHEN OTHERS THEN NULL;
END;");

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

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX ""IX_servers_TenantId_Status"" ON ""servers"" (""TenantId"", ""Status"")';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX ""IX_projects_TenantId_UpdatedAt"" ON ""projects"" (""TenantId"", ""UpdatedAt"")';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX ""IX_pipelines_TenantId_CreatedAt"" ON ""pipelines"" (""TenantId"", ""CreatedAt"")';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX ""IX_deployments_ProjectId_CreatedAt"" ON ""deployments"" (""ProjectId"", ""CreatedAt"")';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX ""IX_deployments_TenantId_CreatedAt"" ON ""deployments"" (""TenantId"", ""CreatedAt"")';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX ""IX_deployments_TenantId_Status_CreatedAt"" ON ""deployments"" (""TenantId"", ""Status"", ""CreatedAt"")';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX ""IX_deployment_logs_DeploymentId_Timestamp"" ON ""deployment_logs"" (""DeploymentId"", ""Timestamp"")';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX ""IX_cost_records_TenantId_RecordedAt"" ON ""cost_records"" (""TenantId"", ""RecordedAt"")';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX ""IX_alerts_TenantId_Source_ResourceId_Status"" ON ""alerts"" (""TenantId"", ""Source"", ""ResourceId"", ""Status"")';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;");

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
