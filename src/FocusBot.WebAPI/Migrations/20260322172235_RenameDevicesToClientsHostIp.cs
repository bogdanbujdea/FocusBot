using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class RenameDevicesToClientsHostIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Devices_DeviceId",
                table: "Sessions");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.RenameColumn(
                name: "DeviceId",
                table: "Sessions",
                newName: "ClientId");

            migrationBuilder.RenameIndex(
                name: "IX_Sessions_DeviceId",
                table: "Sessions",
                newName: "IX_Sessions_ClientId");

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientType = table.Column<int>(type: "integer", nullable: false),
                    Host = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AppVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Platform = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_UserId_Fingerprint",
                table: "Clients",
                columns: new[] { "UserId", "Fingerprint" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Clients_ClientId",
                table: "Sessions",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Clients_ClientId",
                table: "Sessions");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.RenameColumn(
                name: "ClientId",
                table: "Sessions",
                newName: "DeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_Sessions_ClientId",
                table: "Sessions",
                newName: "IX_Sessions_DeviceId");

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeviceType = table.Column<int>(type: "integer", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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
    }
}
