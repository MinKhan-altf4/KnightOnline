using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnightOnline.Server.Migrations
{
    /// <inheritdoc />
    public partial class StarterTutorialVerticalSlice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "objective_progress",
                table: "character_tutorial_progress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_character_tutorial_progress_objective_nonnegative",
                table: "character_tutorial_progress",
                sql: "\"objective_progress\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_character_tutorial_progress_objective_nonnegative",
                table: "character_tutorial_progress");

            migrationBuilder.DropColumn(
                name: "objective_progress",
                table: "character_tutorial_progress");
        }
    }
}
