using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeployFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamInvitationsAndExpandedPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "team_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Email = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Token = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ResendCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    InvitedByName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_invitations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_team_invitations_TenantId",
                table: "team_invitations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_team_invitations_TenantId_Email",
                table: "team_invitations",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_team_invitations_Token",
                table: "team_invitations",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_invitations");
        }
    }
}
