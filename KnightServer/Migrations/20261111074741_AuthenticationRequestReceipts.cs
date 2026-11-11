using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnightOnline.Server.Migrations
{
    /// <inheritdoc />
    public partial class AuthenticationRequestReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authentication_request_receipts",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    protected_result = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authentication_request_receipts", x => x.request_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_authentication_request_receipts_created_at_utc",
                table: "authentication_request_receipts",
                column: "created_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authentication_request_receipts");
        }
    }
}
