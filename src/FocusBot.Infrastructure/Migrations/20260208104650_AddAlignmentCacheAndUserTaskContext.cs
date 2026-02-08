using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlignmentCacheAndUserTaskContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Context",
                table: "UserTasks",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WindowContexts",
                columns: table => new
                {
                    ContextHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    WindowTitle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowContexts", x => x.ContextHash);
                });

            migrationBuilder.CreateTable(
                name: "AlignmentCacheEntries",
                columns: table => new
                {
                    ContextHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TaskContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlignmentCacheEntries", x => new { x.ContextHash, x.TaskContentHash });
                    table.ForeignKey(
                        name: "FK_AlignmentCacheEntries_WindowContexts_ContextHash",
                        column: x => x.ContextHash,
                        principalTable: "WindowContexts",
                        principalColumn: "ContextHash",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlignmentCacheEntries_CreatedAt",
                table: "AlignmentCacheEntries",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlignmentCacheEntries");

            migrationBuilder.DropTable(
                name: "WindowContexts");

            migrationBuilder.DropColumn(
                name: "Context",
                table: "UserTasks");
        }
    }
}
