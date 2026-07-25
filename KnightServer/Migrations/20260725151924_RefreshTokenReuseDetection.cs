using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnightOnline.Server.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenReuseDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "family_id",
                table: "refresh_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "replaced_by_session_id",
                table: "refresh_sessions",
                type: "uuid",
                nullable: true);

            // Every legacy token starts its own family. Using Guid.Empty as a
            // shared default would allow reuse of one token to revoke unrelated
            // accounts.
            migrationBuilder.Sql(
                "UPDATE refresh_sessions SET family_id = id " +
                "WHERE family_id IS NULL");

            migrationBuilder.AlterColumn<Guid>(
                name: "family_id",
                table: "refresh_sessions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_family_id",
                table: "refresh_sessions",
                column: "family_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_sessions_family_id",
                table: "refresh_sessions");

            migrationBuilder.DropColumn(
                name: "family_id",
                table: "refresh_sessions");

            migrationBuilder.DropColumn(
                name: "replaced_by_session_id",
                table: "refresh_sessions");
        }
    }
}
