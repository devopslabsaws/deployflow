using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeployFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AvatarUrlClob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Oracle cannot ALTER a NVARCHAR2 column to CLOB directly.
            // Drop + re-add the column (existing avatar data, if any, is lost — acceptable for dev).
            migrationBuilder.DropColumn(name: "AvatarUrl", table: "users");
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "users",
                type: "CLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AvatarUrl", table: "users");
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "users",
                type: "NVARCHAR2(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
