using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameDescriptionColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "UserSessions",
                newName: "SessionTitle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SessionTitle",
                table: "UserSessions",
                newName: "Description");
        }
    }
}
