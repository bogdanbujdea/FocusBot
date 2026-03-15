using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceStatusWithIsCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "UserTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE UserTasks SET IsCompleted = 1 WHERE Status = 2");

            migrationBuilder.DropIndex(
                name: "IX_UserTasks_Status",
                table: "UserTasks");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "UserTasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "UserTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE UserTasks SET Status = 2 WHERE IsCompleted = 1");
            migrationBuilder.Sql("UPDATE UserTasks SET Status = 0 WHERE IsCompleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_Status",
                table: "UserTasks",
                column: "Status");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "UserTasks");
        }
    }
}
