using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DroppedSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Context = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ContextSwitchCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DistractedSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    DistractionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FocusScorePercent = table.Column<int>(type: "INTEGER", nullable: true),
                    FocusedSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    SessionTitle = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    TopAlignedApps = table.Column<string>(type: "TEXT", nullable: true),
                    TopDistractingApps = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    TotalElapsedSeconds = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.SessionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_IsCompleted",
                table: "UserSessions",
                column: "IsCompleted");
        }
    }
}
