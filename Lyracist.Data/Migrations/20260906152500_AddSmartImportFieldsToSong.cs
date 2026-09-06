// Created on Sep 6, 2026 @ 11:23:00 -> Add migration for Smart Import fields on Song
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lyracist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartImportFieldsToSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "Songs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "Songs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BPM",
                table: "Songs",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VocalPresence",
                table: "Songs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Quality",
                table: "Songs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "BPM",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "VocalPresence",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Quality",
                table: "Songs");
        }
    }
}
