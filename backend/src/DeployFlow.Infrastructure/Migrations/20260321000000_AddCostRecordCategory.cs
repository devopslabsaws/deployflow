using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeployFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCostRecordCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CostRecord.Category removed — ResourceType field reused as category.
            // No schema changes required.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
