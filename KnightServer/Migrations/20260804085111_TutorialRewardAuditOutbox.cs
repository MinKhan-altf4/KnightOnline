using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnightOnline.Server.Migrations
{
    /// <inheritdoc />
    public partial class TutorialRewardAuditOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "domain_outbox_messages",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    event_version = table.Column<int>(type: "integer", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    aggregate_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_outbox_messages", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "gameplay_audit_records",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gameplay_audit_records", x => x.event_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_domain_outbox_messages_published_at_utc_occurred_at_utc",
                table: "domain_outbox_messages",
                columns: new[] { "published_at_utc", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_gameplay_audit_records_character_id_occurred_at_utc",
                table: "gameplay_audit_records",
                columns: new[] { "character_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_gameplay_audit_records_request_id",
                table: "gameplay_audit_records",
                column: "request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "domain_outbox_messages");

            migrationBuilder.DropTable(
                name: "gameplay_audit_records");
        }
    }
}
