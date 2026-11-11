using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnightOnline.Server.Migrations
{
    /// <inheritdoc />
    public partial class AuthenticationReceiptKeyRotationAndExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_authentication_request_receipts_created_at_utc",
                table: "authentication_request_receipts");

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at_utc",
                table: "authentication_request_receipts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP + INTERVAL '1 hour'");

            migrationBuilder.AddColumn<string>(
                name: "key_id",
                table: "authentication_request_receipts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "legacy-v1");

            migrationBuilder.CreateIndex(
                name: "IX_authentication_request_receipts_expires_at_utc",
                table: "authentication_request_receipts",
                column: "expires_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_authentication_request_receipts_expires_at_utc",
                table: "authentication_request_receipts");

            migrationBuilder.DropColumn(
                name: "expires_at_utc",
                table: "authentication_request_receipts");

            migrationBuilder.DropColumn(
                name: "key_id",
                table: "authentication_request_receipts");

            migrationBuilder.CreateIndex(
                name: "IX_authentication_request_receipts_created_at_utc",
                table: "authentication_request_receipts",
                column: "created_at_utc");
        }
    }
}
