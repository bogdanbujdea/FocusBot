using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class RenamedTitleColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Sessions",
                newName: "SessionTitle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SessionTitle",
                table: "Sessions",
                newName: "Title");
        }
    }
}
