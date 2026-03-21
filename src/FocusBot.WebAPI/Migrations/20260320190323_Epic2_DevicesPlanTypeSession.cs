using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class Epic2_DevicesPlanTypeSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ContextSwitchCostSeconds",
                table: "Sessions",
                newName: "ContextSwitchCount");

            migrationBuilder.AddColumn<int>(
                name: "PlanType",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TopAlignedApps",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AppVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Platform = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_DeviceId",
                table: "Sessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId_Fingerprint",
                table: "Devices",
                columns: new[] { "UserId", "Fingerprint" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Devices_DeviceId",
                table: "Sessions",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Devices_DeviceId",
                table: "Sessions");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_DeviceId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PlanType",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "TopAlignedApps",
                table: "Sessions");

            migrationBuilder.RenameColumn(
                name: "ContextSwitchCount",
                table: "Sessions",
                newName: "ContextSwitchCostSeconds");
        }
    }
}
