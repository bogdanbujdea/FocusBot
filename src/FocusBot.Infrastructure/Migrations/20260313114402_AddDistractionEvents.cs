using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDistractionEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "DistractionEvents");
        }
    }
}
