using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnightOnline.Server.Migrations
{
    /// <inheritdoc />
    public partial class CharacterSessionGenerationFence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "current_session_generation",
                table: "characters",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_session_generation",
                table: "characters");
        }
    }
}
