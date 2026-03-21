using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemovedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TopAlignedApps",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "TopDistractingApps",
                table: "Sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TopAlignedApps",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TopDistractingApps",
                table: "Sessions",
                type: "text",
                nullable: true);
        }
    }
}
