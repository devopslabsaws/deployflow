using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeployFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentApprovalAndCanary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Oracle-safe: use BEGIN/EXECUTE IMMEDIATE so we can skip ORA-01430 (column already added)
            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""servers"" ADD ""IsCordoned"" NUMBER(1) DEFAULT 0 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""servers"" ADD ""IsDraining"" NUMBER(1) DEFAULT 0 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""deployments"" ADD ""ApprovalNotes"" NVARCHAR2(1000)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""deployments"" ADD ""ApprovalStatus"" NVARCHAR2(50) DEFAULT ''NotRequired'' NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""deployments"" ADD ""ApprovedAt"" TIMESTAMP(7)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""deployments"" ADD ""ApprovedBy"" RAW(16)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""deployments"" ADD ""CanaryStartedAt"" TIMESTAMP(7)';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""deployments"" ADD ""CanaryStatus"" NVARCHAR2(50) DEFAULT ''None'' NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""deployments"" ADD ""CanaryStepDurationMinutes"" NUMBER(10) DEFAULT 0 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.Sql(@"BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE ""deployments"" ADD ""CanaryTrafficPercent"" NUMBER(10) DEFAULT 0 NOT NULL';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;");

            migrationBuilder.CreateIndex(
                name: "IX_servers_Id_IsCordoned",
                table: "servers",
                columns: new[] { "Id", "IsCordoned" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_servers_Id_IsCordoned",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "IsCordoned",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "IsDraining",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "ApprovalNotes",
                table: "deployments");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "deployments");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "deployments");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "deployments");

            migrationBuilder.DropColumn(
                name: "CanaryStartedAt",
                table: "deployments");

            migrationBuilder.DropColumn(
                name: "CanaryStatus",
                table: "deployments");

            migrationBuilder.DropColumn(
                name: "CanaryStepDurationMinutes",
                table: "deployments");

            migrationBuilder.DropColumn(
                name: "CanaryTrafficPercent",
                table: "deployments");
        }
    }
}
