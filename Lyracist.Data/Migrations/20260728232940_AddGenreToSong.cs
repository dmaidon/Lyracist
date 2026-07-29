// Created on Jul 28, 2026 @ 19:29:40 -> EF Migration to add Genre to Songs
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lyracist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGenreToSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Genre",
                table: "Songs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Genre",
                table: "Songs");
        }
    }
}
