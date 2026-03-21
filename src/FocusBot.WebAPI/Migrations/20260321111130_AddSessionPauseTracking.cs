using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionPauseTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAtUtc",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TotalPausedSeconds",
                table: "Sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PausedAtUtc",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "TotalPausedSeconds",
                table: "Sessions");
        }
    }
}
