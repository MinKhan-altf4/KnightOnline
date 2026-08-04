using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnightOnline.Server.Migrations
{
    /// <inheritdoc />
    public partial class TutorialQuestInventoryAndKillCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_inventory_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    item_definition_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_inventory_items", x => x.id);
                    table.CheckConstraint("ck_character_inventory_quantity_positive", "\"quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_character_inventory_items_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tutorial_commands",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    command_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    result_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tutorial_commands", x => x.request_id);
                    table.ForeignKey(
                        name: "FK_tutorial_commands_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tutorial_kill_credits",
                columns: table => new
                {
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    tutorial_definition_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    monster_life_id = table.Column<Guid>(type: "uuid", nullable: false),
                    monster_definition_id = table.Column<int>(type: "integer", nullable: false),
                    credited_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tutorial_kill_credits", x => new { x.character_id, x.tutorial_definition_id, x.monster_life_id });
                    table.ForeignKey(
                        name: "FK_tutorial_kill_credits_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_inventory_items_character_id_source_type_source_i~",
                table: "character_inventory_items",
                columns: new[] { "character_id", "source_type", "source_id", "item_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tutorial_commands_character_id",
                table: "tutorial_commands",
                column: "character_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_inventory_items");

            migrationBuilder.DropTable(
                name: "tutorial_commands");

            migrationBuilder.DropTable(
                name: "tutorial_kill_credits");
        }
    }
}
