using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeployFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSsoConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SsoClientId",
                table: "tenants",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SsoClientSecret",
                table: "tenants",
                type: "NVARCHAR2(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SsoEnabled",
                table: "tenants",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SsoIssuer",
                table: "tenants",
                type: "NVARCHAR2(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SsoClientId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "SsoClientSecret",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "SsoEnabled",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "SsoIssuer",
                table: "tenants");
        }
    }
}
