using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estimo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BillingUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PlanId",
                table: "UserBillings",
                newName: "Plan");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Plan",
                table: "UserBillings",
                newName: "PlanId");
        }
    }
}
