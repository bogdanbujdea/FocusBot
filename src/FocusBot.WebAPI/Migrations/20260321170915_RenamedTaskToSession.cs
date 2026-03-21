using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class RenamedTaskToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionContext",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "TaskText",
                table: "Sessions");

            migrationBuilder.AddColumn<string>(
                name: "Context",
                table: "Sessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Context",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Sessions");

            migrationBuilder.AddColumn<string>(
                name: "SessionContext",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskText",
                table: "Sessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
