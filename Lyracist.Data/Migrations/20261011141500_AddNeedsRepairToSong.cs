// Created on Oct 11, 2026 @ 10:06:00 -> Migration adding NeedsRepair to Song for skipping unresolvable metadata on rescans
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lyracist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNeedsRepairToSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeedsRepair",
                table: "Songs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE Songs SET NeedsRepair = 1 WHERE (Artist = 'Unknown Artist' OR Artist IS NULL OR Artist = '') AND Duration > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NeedsRepair",
                table: "Songs");
        }
    }
}
