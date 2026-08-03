using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnightOnline.Server.Migrations
{
    /// <inheritdoc />
    public partial class AlphaProgressionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "total_experience",
                table: "characters",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "character_progression_grants",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    requested_experience = table.Column<long>(type: "bigint", nullable: false),
                    applied_experience = table.Column<long>(type: "bigint", nullable: false),
                    level_before = table.Column<int>(type: "integer", nullable: false),
                    level_after = table.Column<int>(type: "integer", nullable: false),
                    total_experience_after = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_progression_grants", x => x.request_id);
                    table.ForeignKey(
                        name: "FK_character_progression_grants_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_characters_level_positive",
                table: "characters",
                sql: "\"level\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_characters_total_experience_nonnegative",
                table: "characters",
                sql: "\"total_experience\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_character_progression_grants_character_id_created_at_utc",
                table: "character_progression_grants",
                columns: new[] { "character_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_progression_grants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_characters_level_positive",
                table: "characters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_characters_total_experience_nonnegative",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "total_experience",
                table: "characters");
        }
    }
}
