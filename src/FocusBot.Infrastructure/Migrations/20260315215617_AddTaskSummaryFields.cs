using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskSummaryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContextSwitchCostSeconds",
                table: "UserTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "DistractedSeconds",
                table: "UserTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "DistractionCount",
                table: "UserTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "FocusedSeconds",
                table: "UserTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TopDistractingApps",
                table: "UserTasks",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextSwitchCostSeconds",
                table: "UserTasks");

            migrationBuilder.DropColumn(
                name: "DistractedSeconds",
                table: "UserTasks");

            migrationBuilder.DropColumn(
                name: "DistractionCount",
                table: "UserTasks");

            migrationBuilder.DropColumn(
                name: "FocusedSeconds",
                table: "UserTasks");

            migrationBuilder.DropColumn(
                name: "TopDistractingApps",
                table: "UserTasks");
        }
    }
}
