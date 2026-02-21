using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFocusScoreAndSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FocusScorePercent",
                table: "UserTasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FocusSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ContextHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AlignmentScore = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    WindowTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocusSegments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FocusSegments_TaskId",
                table: "FocusSegments",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSegments_TaskId_ContextHash_AlignmentScore",
                table: "FocusSegments",
                columns: new[] { "TaskId", "ContextHash", "AlignmentScore" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FocusSegments");
            migrationBuilder.DropColumn(
                name: "FocusScorePercent",
                table: "UserTasks");
        }
    }
}
