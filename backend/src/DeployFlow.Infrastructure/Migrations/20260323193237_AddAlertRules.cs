using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeployFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Metric = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    Operator = table.Column<string>(type: "NVARCHAR2(8)", maxLength: 8, nullable: false),
                    Threshold = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    WindowMinutes = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Severity = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CooldownMinutes = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    Description = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_rules_TenantId",
                table: "alert_rules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_rules_TenantId_IsEnabled",
                table: "alert_rules",
                columns: new[] { "TenantId", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_rules");
        }
    }
}
