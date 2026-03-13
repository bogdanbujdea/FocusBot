using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyFocusAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyFocusAnalytics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnalyticsDateLocal = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalTrackedSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    FocusedSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    UnclearSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    DistractedSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    DistractionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageDistractionSeconds = table.Column<int>(type: "INTEGER", nullable: true)
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
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    WindowTitleSnapshot = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    DistractedDurationSecondsAtEmit = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistractionEvents", x => x.Id);
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyFocusAnalytics");

            migrationBuilder.DropTable(
                name: "DistractionEvents");
        }
    }
}
