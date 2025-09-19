using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estimo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdadateQute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentSessionId",
                table: "Quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Quotes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentUrl",
                table: "Quotes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentSessionId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "PaymentUrl",
                table: "Quotes");
        }
    }
}
