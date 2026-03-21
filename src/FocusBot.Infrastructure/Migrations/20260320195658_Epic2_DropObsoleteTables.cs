using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Epic2_DropObsoleteTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlignmentCacheEntries_WindowContexts_ContextHash",
                table: "AlignmentCacheEntries");

            migrationBuilder.DropTable(
                name: "DailyFocusAnalytics");

            migrationBuilder.DropTable(
                name: "DistractionEvents");

            migrationBuilder.DropTable(
                name: "FocusSegments");

            migrationBuilder.DropTable(
                name: "WindowContexts");

            migrationBuilder.RenameColumn(
                name: "ContextSwitchCostSeconds",
                table: "UserTasks",
                newName: "ContextSwitchCount");

            migrationBuilder.AddColumn<string>(
                name: "TopAlignedApps",
                table: "UserTasks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TopAlignedApps",
                table: "UserTasks");

            migrationBuilder.RenameColumn(
                name: "ContextSwitchCount",
                table: "UserTasks",
                newName: "ContextSwitchCostSeconds");

            migrationBuilder.CreateTable(
                name: "DailyFocusAnalytics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnalyticsDateLocal = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AverageDistractionSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    DistractedSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    DistractionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FocusedSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalTrackedSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    UnclearSeconds = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyFocusAnalytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DistractionEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DistractedDurationSecondsAtEmit = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    WindowTitleSnapshot = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistractionEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FocusSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlignmentScore = table.Column<int>(type: "INTEGER", nullable: false),
                    AnalyticsDateLocal = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ContextHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    WindowTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocusSegments", x => x.Id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_DailyFocusAnalytics_AnalyticsDateLocal",
                table: "DailyFocusAnalytics",
                column: "AnalyticsDateLocal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DistractionEvents_SessionId_OccurredAtUtc",
                table: "DistractionEvents",
                columns: new[] { "SessionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DistractionEvents_TaskId_OccurredAtUtc",
                table: "DistractionEvents",
                columns: new[] { "TaskId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FocusSegments_TaskId",
                table: "FocusSegments",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSegments_TaskId_ContextHash_AlignmentScore_AnalyticsDateLocal",
                table: "FocusSegments",
                columns: new[] { "TaskId", "ContextHash", "AlignmentScore", "AnalyticsDateLocal" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AlignmentCacheEntries_WindowContexts_ContextHash",
                table: "AlignmentCacheEntries",
                column: "ContextHash",
                principalTable: "WindowContexts",
                principalColumn: "ContextHash",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
