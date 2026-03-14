using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDateToFocusSegment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FocusSegments_TaskId_ContextHash_AlignmentScore",
                table: "FocusSegments");

            var today = DateOnly.FromDateTime(DateTime.Now);
            migrationBuilder.AddColumn<DateOnly>(
                name: "AnalyticsDateLocal",
                table: "FocusSegments",
                type: "TEXT",
                nullable: false,
                defaultValue: today);

            migrationBuilder.CreateIndex(
                name: "IX_FocusSegments_TaskId_ContextHash_AlignmentScore_AnalyticsDateLocal",
                table: "FocusSegments",
                columns: new[] { "TaskId", "ContextHash", "AlignmentScore", "AnalyticsDateLocal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FocusSegments_TaskId_ContextHash_AlignmentScore_AnalyticsDateLocal",
                table: "FocusSegments");

            migrationBuilder.DropColumn(
                name: "AnalyticsDateLocal",
                table: "FocusSegments");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSegments_TaskId_ContextHash_AlignmentScore",
                table: "FocusSegments",
                columns: new[] { "TaskId", "ContextHash", "AlignmentScore" },
                unique: true);
        }
    }
}
