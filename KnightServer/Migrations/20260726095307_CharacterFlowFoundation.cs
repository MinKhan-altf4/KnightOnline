using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnightOnline.Server.Migrations
{
    /// <inheritdoc />
    public partial class CharacterFlowFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_characters_account_id",
                table: "characters");

            migrationBuilder.DropIndex(
                name: "IX_characters_normalized_name",
                table: "characters");

            migrationBuilder.AddColumn<string>(
                name: "body_type_definition_id",
                table: "characters",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "current_class_definition_id",
                table: "characters",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "current_map_definition_id",
                table: "characters",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "current_spawn_point_id",
                table: "characters",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "position_x",
                table: "characters",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "position_y",
                table: "characters",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<string>(
                name: "server_id",
                table: "characters",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "slot_index",
                table: "characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "character_appearances",
                columns: table => new
                {
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    slot_definition_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    appearance_definition_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_appearances", x => new { x.character_id, x.slot_definition_id });
                    table.ForeignKey(
                        name: "FK_character_appearances_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_creation_requests",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: true),
                    result_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result_message = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_creation_requests", x => x.request_id);
                    table.ForeignKey(
                        name: "FK_character_creation_requests_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_character_creation_requests_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "character_tutorial_progress",
                columns: table => new
                {
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    tutorial_definition_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    current_step_definition_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<byte>(type: "smallint", nullable: false),
                    continue_choice = table.Column<bool>(type: "boolean", nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_tutorial_progress", x => new { x.character_id, x.tutorial_definition_id });
                    table.ForeignKey(
                        name: "FK_character_tutorial_progress_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                WITH ranked_characters AS
                (
                    SELECT id,
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY account_id
                               ORDER BY id
                           ) AS generated_slot
                    FROM characters
                )
                UPDATE characters AS character
                SET server_id = 'server-1',
                    slot_index = ranked.generated_slot,
                    current_class_definition_id = 'warrior',
                    body_type_definition_id = 'male',
                    current_map_definition_id = 'tutorial_map_01',
                    current_spawn_point_id = 'tutorial_spawn_default',
                    version = 1
                FROM ranked_characters AS ranked
                WHERE character.id = ranked.id;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO character_appearances
                    (character_id, slot_definition_id,
                     appearance_definition_id, updated_at_utc, version)
                SELECT id, seed.slot_definition_id,
                       seed.appearance_definition_id, created_at_utc, 1
                FROM characters
                CROSS JOIN
                (
                    VALUES
                        ('base_body', 'body_male_001'),
                        ('hair', 'hair_001'),
                        ('bottom', 'bottom_001'),
                        ('expression', 'expression_001')
                ) AS seed(slot_definition_id, appearance_definition_id);

                INSERT INTO character_tutorial_progress
                    (character_id, tutorial_definition_id,
                     current_step_definition_id, state,
                     updated_at_utc, version)
                SELECT id, 'starter_tutorial_v1', 'welcome', 0,
                       created_at_utc, 1
                FROM characters;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_characters_account_id_server_id_slot_index",
                table: "characters",
                columns: new[] { "account_id", "server_id", "slot_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_characters_server_id_normalized_name",
                table: "characters",
                columns: new[] { "server_id", "normalized_name" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_characters_slot_index",
                table: "characters",
                sql: "\"slot_index\" BETWEEN 1 AND 3");

            migrationBuilder.CreateIndex(
                name: "IX_character_creation_requests_account_id",
                table: "character_creation_requests",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_character_creation_requests_character_id",
                table: "character_creation_requests",
                column: "character_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_appearances");

            migrationBuilder.DropTable(
                name: "character_creation_requests");

            migrationBuilder.DropTable(
                name: "character_tutorial_progress");

            migrationBuilder.DropIndex(
                name: "IX_characters_account_id_server_id_slot_index",
                table: "characters");

            migrationBuilder.DropIndex(
                name: "IX_characters_server_id_normalized_name",
                table: "characters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_characters_slot_index",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "body_type_definition_id",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "current_class_definition_id",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "current_map_definition_id",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "current_spawn_point_id",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "position_x",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "position_y",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "server_id",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "slot_index",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "version",
                table: "characters");

            migrationBuilder.CreateIndex(
                name: "IX_characters_account_id",
                table: "characters",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_normalized_name",
                table: "characters",
                column: "normalized_name",
                unique: true);
        }
    }
}
