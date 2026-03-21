using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamedTaskToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserTasks",
                table: "UserTasks");

            migrationBuilder.RenameTable(
                name: "UserTasks",
                newName: "UserSessions");

            migrationBuilder.RenameColumn(
                name: "TaskId",
                table: "UserSessions",
                newName: "SessionId");

            migrationBuilder.RenameIndex(
                name: "IX_UserTasks_IsCompleted",
                table: "UserSessions",
                newName: "IX_UserSessions_IsCompleted");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserSessions",
                table: "UserSessions",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserSessions",
                table: "UserSessions");

            migrationBuilder.RenameTable(
                name: "UserSessions",
                newName: "UserTasks");

            migrationBuilder.RenameColumn(
                name: "SessionId",
                table: "UserTasks",
                newName: "TaskId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSessions_IsCompleted",
                table: "UserTasks",
                newName: "IX_UserTasks_IsCompleted");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserTasks",
                table: "UserTasks",
                column: "TaskId");
        }
    }
}
